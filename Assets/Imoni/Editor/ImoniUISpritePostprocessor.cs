// みんなで芋煮会 UIスプライトの自動インポート設定
// このファイルを Assets/Editor/ に、PNG を Assets/Imoni/Art/Sprites/UI/ フォルダに置くと、
// テクスチャ設定と 9スライスの境界(Border)が自動で入ります。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Imoni.Editor
{
public class ImoniUISpritePostprocessor : AssetPostprocessor
{
    // 画像は 3倍(@3x)で書き出し済み。値はピクセル。Vector4 の順番は Unity 仕様の (左, 下, 右, 上)
    static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "btn_pill_green", new Vector4(96, 111, 96, 96) }, // L,B,R,T
        { "btn_pill_red", new Vector4(96, 111, 96, 96) }, // L,B,R,T
        { "btn_pill_yellow", new Vector4(96, 111, 96, 96) }, // L,B,R,T
        { "btn_pill_outline", new Vector4(66, 63, 66, 63) }, // L,B,R,T
        { "btn_square_yellow", new Vector4(51, 51, 51, 51) }, // L,B,R,T
        { "btn_square_green", new Vector4(51, 51, 51, 51) }, // L,B,R,T
        { "card_shadow", new Vector4(78, 96, 78, 78) }, // L,B,R,T
        { "card_cream", new Vector4(57, 57, 57, 57) }, // L,B,R,T
        { "card_white", new Vector4(57, 57, 57, 57) }, // L,B,R,T
        { "sheet_top", new Vector4(90, 0, 90, 90) }, // L,B,R,T
        { "bar_bg", new Vector4(27, 24, 27, 24) }, // L,B,R,T
        { "bar_fill", new Vector4(18, 15, 18, 15) }, // L,B,R,T
        { "input_field", new Vector4(33, 33, 33, 33) }, // L,B,R,T
        { "pill_white", new Vector4(60, 57, 60, 57) }, // L,B,R,T
        { "round6_white", new Vector4(18, 18, 18, 18) }, // L,B,R,T
        { "round12_white", new Vector4(36, 36, 36, 36) }, // L,B,R,T
        { "circle_white", Vector4.zero },
        { "circle_avatar", Vector4.zero },
        { "circle_outline", Vector4.zero },
    };

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Imoni/Art/Sprites/UI/")) return;
        var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        if (!Borders.TryGetValue(name, out var border)) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // 9スライスに必要
        ti.SetTextureSettings(settings);

        ti.spriteBorder = border;
    }
}

}
