import Foundation
import Network
import os

// File-level constants — accessible from any isolation domain
private let kHeaderSize = 5
private let kMaxPayload = 100_000_000  // 100 MB

/// Thread-safe container for AsyncStream continuations.
/// Separate from @Observable to avoid macro conflicts with nonisolated storage.
private final class StreamContinuations: @unchecked Sendable {
    var mesh: AsyncStream<MeshData>.Continuation?
    var instance: AsyncStream<InstanceData>.Continuation?
}

/// TCP client for receiving mesh data from Blender.
/// Port of Unity/GeometrySync/.../MeshStreamClient.cs using Network.framework + AsyncStream.
@MainActor @Observable
final class MeshStreamClient {

    // MARK: - Public state

    private(set) var isConnected = false
    private(set) var lastError: String?

    // Stats — updated once per second to avoid triggering excessive SwiftUI redraws
    private(set) var bytesReceived: UInt64 = 0
    private(set) var meshUpdateCount: UInt64 = 0
    private(set) var instanceUpdateCount: UInt64 = 0
    private(set) var currentFPS: Double = 0

    // Internal counters (not @Observable, no SwiftUI overhead)
    private var _bytesAccum: UInt64 = 0
    private var _meshCountAccum: UInt64 = 0
    private var _instanceCountAccum: UInt64 = 0
    private var _frameCount: Int = 0
    private var _lastStatsFlush: CFAbsoluteTime = 0

    // MARK: - Configuration

    let host: String
    let port: UInt16

    // MARK: - Streams (created once in init)

    let meshStream: AsyncStream<MeshData>
    let instanceStream: AsyncStream<InstanceData>

    // MARK: - Private

    private let logger = Logger(subsystem: "com.geometrysync.visionpro", category: "Network")
    private let queue = DispatchQueue(label: "com.geometrysync.network", qos: .userInitiated)
    private var connection: NWConnection?
    private var isRunning = false
    private let continuations = StreamContinuations()

    // MARK: - Init

    init(host: String = "127.0.0.1", port: UInt16 = 8080) {
        self.host = host
        self.port = port

        var meshCont: AsyncStream<MeshData>.Continuation!
        self.meshStream = AsyncStream(bufferingPolicy: .bufferingNewest(2)) { continuation in
            meshCont = continuation
        }

        var instanceCont: AsyncStream<InstanceData>.Continuation!
        self.instanceStream = AsyncStream(bufferingPolicy: .bufferingNewest(1)) { continuation in
            instanceCont = continuation
        }

        self.continuations.mesh = meshCont
        self.continuations.instance = instanceCont
    }

    // MARK: - Connection lifecycle

    func connect() {
        guard !isRunning else {
            logger.warning("Client already running")
            return
        }
        isRunning = true
        lastError = nil
        startConnection()
    }

    func disconnect() {
        isRunning = false
        connection?.cancel()
        connection = nil
        isConnected = false
        continuations.mesh?.finish()
        continuations.instance?.finish()
        continuations.mesh = nil
        continuations.instance = nil
        logger.info("Client disconnected")
    }

    // MARK: - Stats (throttled to 1Hz to avoid SwiftUI churn)

    private func recordMessage(bytes: Int, meshCount: Int, instanceCount: Int) {
        _bytesAccum += UInt64(bytes)
        _meshCountAccum += UInt64(meshCount)
        _instanceCountAccum += UInt64(instanceCount)
        _frameCount += 1

        let now = CFAbsoluteTimeGetCurrent()
        let elapsed = now - _lastStatsFlush
        if elapsed >= 1.0 {
            // Flush to @Observable properties once per second
            bytesReceived += _bytesAccum
            meshUpdateCount += _meshCountAccum
            instanceUpdateCount += _instanceCountAccum
            currentFPS = Double(_frameCount) / elapsed

            _bytesAccum = 0
            _meshCountAccum = 0
            _instanceCountAccum = 0
            _frameCount = 0
            _lastStatsFlush = now
        }
    }

    // MARK: - NWConnection management

    private func startConnection() {
        let tcpOptions = NWProtocolTCP.Options()
        tcpOptions.noDelay = true

        let params = NWParameters(tls: nil, tcp: tcpOptions)
        let endpoint = NWEndpoint.hostPort(
            host: NWEndpoint.Host(host),
            port: NWEndpoint.Port(rawValue: port)!
        )

        let conn = NWConnection(to: endpoint, using: params)
        self.connection = conn

        conn.stateUpdateHandler = { [weak self] state in
            guard let self else { return }
            switch state {
            case .ready:
                Task { @MainActor in
                    self.isConnected = true
                    self.lastError = nil
                }
                self.logger.info("Connected to \(self.host):\(self.port)")
                self.receiveNextMessage(on: conn)

            case .failed(let error):
                Task { @MainActor in
                    self.isConnected = false
                    self.lastError = error.localizedDescription
                    self.scheduleReconnect()
                }
                self.logger.error("Connection failed: \(error)")

            case .cancelled:
                Task { @MainActor in
                    self.isConnected = false
                }

            case .waiting(let error):
                Task { @MainActor in
                    self.lastError = "Waiting: \(error.localizedDescription)"
                }
                self.logger.info("Connection waiting: \(error)")

            default:
                break
            }
        }

        conn.start(queue: queue)
    }

    private func scheduleReconnect() {
        guard isRunning else { return }
        logger.info("Reconnecting in 2 seconds...")
        queue.asyncAfter(deadline: .now() + 2.0) { [weak self] in
            guard let self else { return }
            Task { @MainActor in
                guard self.isRunning else { return }
                self.startConnection()
            }
        }
    }

    // MARK: - Receive loop

    private nonisolated func receiveNextMessage(on conn: NWConnection) {
        let conts = self.continuations
        let log = self.logger

        conn.receive(minimumIncompleteLength: kHeaderSize,
                     maximumLength: kHeaderSize) { [weak self] content, _, isComplete, error in
            guard let self else { return }

            if let error {
                log.error("Header read error: \(error)")
                self.handleDisconnect(conn)
                return
            }

            if isComplete && (content == nil || content!.isEmpty) {
                log.info("Connection closed by server")
                self.handleDisconnect(conn)
                return
            }

            guard let headerData = content, headerData.count == kHeaderSize else {
                log.warning("Incomplete header")
                self.handleDisconnect(conn)
                return
            }

            let messageType = headerData[headerData.startIndex]
            let payloadLength = headerData.withUnsafeBytes { raw -> Int in
                let bits = raw.loadUnaligned(fromByteOffset: 1, as: UInt32.self)
                return Int(UInt32(littleEndian: bits))
            }

            guard payloadLength > 0, payloadLength <= kMaxPayload else {
                log.error("Invalid payload length: \(payloadLength)")
                self.handleDisconnect(conn)
                return
            }

            self.receiveExactly(conn, length: payloadLength) { payloadData in
                guard let payload = payloadData else {
                    log.warning("Failed to read payload")
                    self.handleDisconnect(conn)
                    return
                }

                let totalBytes = kHeaderSize + payloadLength
                Self.processMessage(type: messageType, payload: payload,
                                    continuations: conts, logger: log)
                { meshCount, instanceCount in
                    Task { @MainActor in
                        self.recordMessage(bytes: totalBytes,
                                           meshCount: meshCount,
                                           instanceCount: instanceCount)
                    }
                }

                self.receiveNextMessage(on: conn)
            }
        }
    }

    /// Read exactly `length` bytes, accumulating partial reads.
    private nonisolated func receiveExactly(
        _ conn: NWConnection,
        length: Int,
        accumulated: Data = Data(),
        completion: @escaping @Sendable (Data?) -> Void
    ) {
        let remaining = length - accumulated.count
        guard remaining > 0 else {
            completion(accumulated)
            return
        }

        conn.receive(minimumIncompleteLength: remaining,
                     maximumLength: remaining) { [weak self] content, _, isComplete, error in
            if let error {
                self?.logger.error("Payload read error: \(error)")
                completion(nil)
                return
            }

            if isComplete && (content == nil || content!.isEmpty) {
                completion(nil)
                return
            }

            var data = accumulated
            if let chunk = content {
                data.append(contentsOf: chunk)
            }

            if data.count >= length {
                completion(data)
            } else {
                self?.receiveExactly(conn, length: length, accumulated: data, completion: completion)
            }
        }
    }

    // MARK: - Message processing (static — no isolation needed)

    private nonisolated static func processMessage(
        type: UInt8,
        payload: Data,
        continuations: StreamContinuations,
        logger: Logger,
        updateCounts: @Sendable @escaping (_ meshCount: Int, _ instanceCount: Int) -> Void
    ) {
        do {
            switch type {
            case MessageType.mesh.rawValue:
                let meshData = try MeshDeserializer.deserializeMesh(payload)
                logger.debug("Mesh: \(meshData.vertexCount) verts, \(meshData.triangleCount) tris")
                continuations.mesh?.yield(meshData)
                updateCounts(1, 0)

            case MessageType.instance.rawValue:
                let instanceData = try MeshDeserializer.deserializeInstances(payload)
                logger.debug("Instances: \(instanceData.instanceCount) for mesh \(instanceData.meshId)")
                continuations.instance?.yield(instanceData)
                updateCounts(0, 1)

            case MessageType.delta.rawValue:
                logger.warning("Delta updates not yet implemented")

            default:
                logger.warning("Unknown message type: 0x\(String(type, radix: 16))")
            }
        } catch {
            logger.error("Deserialization error: \(error.localizedDescription)")
        }
    }

    private nonisolated func handleDisconnect(_ conn: NWConnection) {
        conn.cancel()
        Task { @MainActor [weak self] in
            guard let self else { return }
            self.isConnected = false
            self.scheduleReconnect()
        }
    }
}
