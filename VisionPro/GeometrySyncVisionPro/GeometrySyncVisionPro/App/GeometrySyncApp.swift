import SwiftUI

/// Shared app state accessible from both WindowGroup and ImmersiveSpace scenes.
@MainActor @Observable
final class AppModel {
    var client: MeshStreamClient?
}

@main
struct GeometrySyncApp: App {
    @State private var appModel = AppModel()

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environment(appModel)
        }

        ImmersiveSpace(id: "geometrySync") {
            ImmersiveView()
                .environment(appModel)
        }
        .immersionStyle(selection: .constant(.mixed), in: .mixed)
    }
}
