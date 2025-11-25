using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace GeometrySync
{
    /// <summary>
    /// TCP client for receiving mesh data from Blender
    /// </summary>
    public class MeshStreamClient : IDisposable
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private Thread _receiveThread;
        private bool _isRunning;
        private readonly ConcurrentQueue<MeshData> _meshQueue;
        private readonly ConcurrentQueue<InstanceData> _instanceQueue;
        private readonly string _host;
        private readonly int _port;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public int QueuedMeshCount => _meshQueue.Count;
        public int QueuedInstanceCount => _instanceQueue.Count;

        public MeshStreamClient(string host = "127.0.0.1", int port = 8080)
        {
            _host = host;
            _port = port;
            _meshQueue = new ConcurrentQueue<MeshData>();
            _instanceQueue = new ConcurrentQueue<InstanceData>();
        }

        /// <summary>
        /// Start the client and connect to Blender server
        /// </summary>
        public void Connect()
        {
            if (_isRunning)
            {
                Debug.LogWarning("Client already running");
                return;
            }

            _isRunning = true;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "GeometrySync Receiver"
            };
            _receiveThread.Start();

            Debug.Log($"GeometrySync client started, connecting to {_host}:{_port}");
        }

        /// <summary>
        /// Stop the client and disconnect
        /// </summary>
        public void Disconnect()
        {
            _isRunning = false;

            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing connection: {e.Message}");
            }

            _receiveThread?.Join(TimeSpan.FromSeconds(2));

            Debug.Log("GeometrySync client stopped");
        }

        /// <summary>
        /// Try to get the next mesh from the queue
        /// </summary>
        public bool TryGetMesh(out MeshData meshData)
        {
            return _meshQueue.TryDequeue(out meshData);
        }

        /// <summary>
        /// Try to get the next instance data from the queue (Phase 2: GPU Instancing)
        /// </summary>
        public bool TryGetInstanceData(out InstanceData instanceData)
        {
            return _instanceQueue.TryDequeue(out instanceData);
        }

        /// <summary>
        /// Clear the mesh queue
        /// </summary>
        public void ClearQueue()
        {
            while (_meshQueue.TryDequeue(out _)) { }
            while (_instanceQueue.TryDequeue(out _)) { }
        }

        private void ReceiveLoop()
        {
            while (_isRunning)
            {
                try
                {
                    // Try to connect
                    _tcpClient = new TcpClient();
                    _tcpClient.NoDelay = true; // Enable TCP_NODELAY for reduced latency
                    _tcpClient.Connect(_host, _port);
                    _stream = _tcpClient.GetStream();
                    _isConnected = true;

                    Debug.Log($"Connected to Blender server at {_host}:{_port}");

                    // Receive loop
                    while (_isRunning && _tcpClient.Connected)
                    {
                        // Read message header: [type:1byte][length:4bytes]
                        byte[] header = new byte[5];
                        int bytesRead = ReadExactly(_stream, header, 0, 5);

                        if (bytesRead != 5)
                        {
                            Debug.LogWarning("Failed to read message header");
                            break;
                        }

                        byte messageType = header[0];
                        int payloadLength = BitConverter.ToInt32(header, 1);

                        // Validate payload length
                        if (payloadLength <= 0 || payloadLength > 100_000_000) // 100MB max
                        {
                            Debug.LogError($"Invalid payload length: {payloadLength}");
                            break;
                        }

                        // Read payload
                        byte[] payload = new byte[payloadLength];
                        bytesRead = ReadExactly(_stream, payload, 0, payloadLength);

                        if (bytesRead != payloadLength)
                        {
                            Debug.LogWarning($"Failed to read complete payload. Expected {payloadLength}, got {bytesRead}");
                            break;
                        }

                        // Process message based on type
                        switch (messageType)
                        {
                            case 0x01: // Full mesh update
                                ProcessMeshData(payload);
                                break;

                            case 0x02: // Instance data (Phase 2: GPU Instancing)
                                ProcessInstanceData(payload);
                                break;

                            case 0x03: // Delta update (future - reserved)
                                Debug.LogWarning("Delta updates not yet implemented");
                                break;

                            default:
                                Debug.LogWarning($"Unknown message type: {messageType:X2}");
                                break;
                        }
                    }
                }
                catch (SocketException e)
                {
                    _isConnected = false;
                    Debug.LogWarning($"Connection error: {e.Message}. Retrying in 2 seconds...");
                    Thread.Sleep(2000);
                }
                catch (Exception e)
                {
                    _isConnected = false;
                    Debug.LogError($"Receive error: {e}");
                    Thread.Sleep(2000);
                }
                finally
                {
                    _stream?.Close();
                    _tcpClient?.Close();
                    _isConnected = false;
                }
            }
        }

        /// <summary>
        /// Read exactly the specified number of bytes from the stream
        /// </summary>
        private int ReadExactly(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    return totalRead; // Connection closed
                }
                totalRead += read;
            }
            return totalRead;
        }

        private void ProcessMeshData(byte[] data)
        {
            try
            {
                Debug.Log($"[MeshStreamClient] Received mesh data: {data.Length} bytes");
                MeshData meshData = MeshDeserializer.Deserialize(data);
                Debug.Log($"[MeshStreamClient] Deserialized: {meshData.VertexCount} vertices, {meshData.Indices.Length} indices");

                // Add to queue, but limit queue size
                _meshQueue.Enqueue(meshData);

                // Drop old frames if queue gets too large
                while (_meshQueue.Count > 2)
                {
                    _meshQueue.TryDequeue(out _);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize mesh data: {e}");
            }
        }

        private void ProcessInstanceData(byte[] data)
        {
            try
            {
                Debug.Log($"[MeshStreamClient] Received instance data: {data.Length} bytes");
                InstanceData instanceData = MeshDeserializer.DeserializeInstanceData(data);
                Debug.Log($"[MeshStreamClient] Deserialized: {instanceData.InstanceCount} instances for mesh ID {instanceData.MeshId}");

                // Add to queue, but limit queue size
                _instanceQueue.Enqueue(instanceData);

                // Drop old frames if queue gets too large
                while (_instanceQueue.Count > 5)
                {
                    _instanceQueue.TryDequeue(out _);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize instance data: {e}");
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
