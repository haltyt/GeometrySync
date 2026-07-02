import RealityKit
import UIKit
import simd

/// Builds a RealityKit PhysicallyBasedMaterial from streamed Blender
/// Principled BSDF parameters (message 0x04).
///
/// Note: visionOS has no CustomMaterial (Metal surface shaders), but
/// PhysicallyBasedMaterial covers the full M1 parameter set. Roughness is
/// the same convention as Blender — no inversion (unlike Unity smoothness).
extension MaterialData {

    /// Blender sends linear color values; UIColor expects sRGB components.
    private static func srgb(_ linear: Float) -> CGFloat {
        let x = Double(max(0, min(1, linear)))
        return CGFloat(x <= 0.0031308 ? x * 12.92 : 1.055 * pow(x, 1.0 / 2.4) - 0.055)
    }

    func makeMaterial() -> RealityKit.Material {
        var pbr = PhysicallyBasedMaterial()

        let opacity = max(0, min(1, baseColor.w * alpha))
        let tint = UIColor(
            red: Self.srgb(baseColor.x),
            green: Self.srgb(baseColor.y),
            blue: Self.srgb(baseColor.z),
            alpha: CGFloat(opacity))
        pbr.baseColor = .init(tint: tint)

        pbr.metallic = .init(floatLiteral: max(0, min(1, metallic)))
        pbr.roughness = .init(floatLiteral: max(0, min(1, roughness)))

        if emissionStrength > 0, emission.max() > 0 {
            pbr.emissiveColor = .init(color: UIColor(
                red: Self.srgb(emission.x),
                green: Self.srgb(emission.y),
                blue: Self.srgb(emission.z),
                alpha: 1))
            pbr.emissiveIntensity = emissionStrength
        }

        if opacity < 0.999 {
            pbr.blending = .transparent(opacity: .init(floatLiteral: opacity))
        }

        return pbr
    }
}
