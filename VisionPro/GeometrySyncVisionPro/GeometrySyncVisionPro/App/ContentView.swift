import SwiftUI

struct ContentView: View {
    @Environment(AppModel.self) private var appModel
    @State private var host: String = "127.0.0.1"
    @State private var port: String = "8080"
    @State private var immersiveSpaceOpen = false

    @Environment(\.openImmersiveSpace) private var openImmersiveSpace
    @Environment(\.dismissImmersiveSpace) private var dismissImmersiveSpace

    private var client: MeshStreamClient? { appModel.client }

    var body: some View {
        VStack(spacing: 20) {
            Text("GeometrySync")
                .font(.largeTitle)
                .fontWeight(.bold)

            Text("Blender Mesh Streaming")
                .font(.title3)
                .foregroundStyle(.secondary)

            Divider()

            // Connection settings
            GroupBox("Connection") {
                VStack(alignment: .leading, spacing: 12) {
                    HStack {
                        Text("Host:")
                            .frame(width: 50, alignment: .trailing)
                        TextField("IP Address", text: $host)
                            .textFieldStyle(.roundedBorder)
                            .disabled(client?.isConnected == true)
                    }

                    HStack {
                        Text("Port:")
                            .frame(width: 50, alignment: .trailing)
                        TextField("Port", text: $port)
                            .textFieldStyle(.roundedBorder)
                            .disabled(client?.isConnected == true)
                    }
                }
                .padding(.vertical, 4)
            }

            // Connect / Disconnect
            HStack(spacing: 16) {
                if client?.isConnected == true {
                    Button("Disconnect") {
                        appModel.client?.disconnect()
                        appModel.client = nil
                    }
                    .tint(.red)
                } else {
                    Button("Connect") {
                        let p = UInt16(port) ?? 8080
                        let newClient = MeshStreamClient(host: host, port: p)
                        appModel.client = newClient
                        newClient.connect()
                    }
                    .tint(.blue)
                }
            }
            .buttonStyle(.borderedProminent)

            // Status
            GroupBox("Status") {
                VStack(alignment: .leading, spacing: 8) {
                    statusRow("Connection",
                              value: client?.isConnected == true ? "Connected" : "Disconnected",
                              color: client?.isConnected == true ? .green : .red)

                    if let error = client?.lastError {
                        statusRow("Error", value: error, color: .orange)
                    }

                    if let client {
                        statusRow("FPS", value: String(format: "%.1f", client.currentFPS),
                                  color: client.currentFPS >= 20 ? .green : .orange)
                        statusRow("Bytes Received", value: formatBytes(client.bytesReceived))
                        statusRow("Mesh Updates", value: "\(client.meshUpdateCount)")
                        statusRow("Instance Updates", value: "\(client.instanceUpdateCount)")
                    }
                }
                .padding(.vertical, 4)
            }

            Divider()

            // Immersive Space toggle
            Button(immersiveSpaceOpen ? "Close Immersive View" : "Open Immersive View") {
                Task {
                    if immersiveSpaceOpen {
                        await dismissImmersiveSpace()
                        immersiveSpaceOpen = false
                    } else {
                        let result = await openImmersiveSpace(id: "geometrySync")
                        immersiveSpaceOpen = result == .opened
                    }
                }
            }
            .buttonStyle(.borderedProminent)
            .tint(immersiveSpaceOpen ? .orange : .green)
            .disabled(client == nil)

            Spacer()
        }
        .padding(30)
        .frame(minWidth: 400, minHeight: 500)
    }

    // MARK: - Helpers

    private func statusRow(_ label: String, value: String, color: Color = .primary) -> some View {
        HStack {
            Text("\(label):")
                .foregroundStyle(.secondary)
            Spacer()
            Text(value)
                .foregroundStyle(color)
                .fontDesign(.monospaced)
        }
    }

    private func formatBytes(_ bytes: UInt64) -> String {
        if bytes < 1024 { return "\(bytes) B" }
        if bytes < 1024 * 1024 { return String(format: "%.1f KB", Double(bytes) / 1024) }
        return String(format: "%.1f MB", Double(bytes) / (1024 * 1024))
    }
}
