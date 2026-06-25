<div align="center">

# FigmaToUnity

### Import Figma design into Unity UI — straight from the Editor

**Turn Figma frames into UGUI hierarchies or UIToolkit (UXML + USS)** — sprites, text, auto-layout, 9-slice, prefabs, and incremental diff-updates.

![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity&logoColor=white)
![Backends](https://img.shields.io/badge/backends-UGUI%20%7C%20UIToolkit-blue)
![Package](https://img.shields.io/badge/UPM-com.longgames.figma--to--unity-orange)
![Version](https://img.shields.io/badge/version-0.1.1-informational)
![License](https://img.shields.io/badge/license-MIT-green)

</div>

---

## ✨ Features

- **Two output backends** — generate a **UGUI** scene/prefab hierarchy or **UIToolkit** UXML + USS.
- **Manual tags** — append `#image`, `#slice9`, `#button`, `#scroll`, `#mask`, `#prefab`, … to a node's name to control how it imports.
- **Smart tagging** — auto-detects buttons, scroll viewport/content, and 3×3 9-slice grids.
- **Incremental diff-update** — re-importing matches existing nodes by id; user-edited elements (and your `.uss` wrappers) are preserved.
- **Font resolution** — multi-stage TMP font matching with a configurable fallback chain and optional CJK system-font fallback.

## 📦 Requirements

- Unity **2022.3** or newer
- A Figma **personal access token**

## 🚀 Installation

Install as a [UPM](https://docs.unity3d.com/Manual/upm-ui.html) package via **Window → Package Manager**:

- **Add package from git URL…** — paste:
  ```
  https://github.com/natsu1211/FigmaToUnity.git?path=FigmaToUnity
  ```
- or **Add package from disk…** — select `FigmaToUnity/package.json`

The package id is `com.longgames.figma-to-unity`.

## 🏁 Usage

First, create a Figma Personal Access Token from your Figma account settings.

Then open `LongGames > Figma Importer` from the Unity Editor menu:

1. Paste your token into the **Figma Auth** token field and press **Verify Token**.
   - **Connected**: your account name is shown — authentication succeeded.
2. Enter your Figma design URL in the project's **Figma URL** field and press **Fetch Frames**.
3. Select the frames you want and press **Import Selected**.

<img width="503" height="197" alt="image" src="https://github.com/user-attachments/assets/2cfd877e-ffe4-4ee0-96e6-7030e422b8fa" />
<img width="503" height="185" alt="image" src="https://github.com/user-attachments/assets/fc25c5ac-1bed-47b1-9979-ce928b786236" />


### Output Backend

Choose the destination with the **Output Backend** setting in the window:

| Value | Output |
|---|---|
| `UGUI` | UGUI scene / prefab hierarchy (default) |
| `UIToolkit` | UXML + USS under `Assets/UI/<file>/`. `<frame>.generated.uss` is overwritten every run; the `<frame>.uss` wrapper is created only once and never touched again (so your edits survive). Diff-update matches existing UXML by the `figma-id` attribute, and elements you added by hand (no `figma-id`) are re-anchored to their preceding figma sibling. |

### Figma token storage

The Personal Access Token you enter in the Editor window is stored per-user via Unity's `EditorPrefs` — it lives on your machine, is **never written into the project**, and so is never committed. All other importer settings are saved in `ProjectSettings/LongGames.FigmaImporter.asset`.

## 🏷️ Manual tags

Append `#tagname` to the end of a Figma node name to force a specific Unity component or structure. Multiple tags can be combined (e.g. `panel #image #ignore`). Only the token immediately after `#` is recognized, separated by whitespace, comma, or semicolon.

| Tag | Effect |
|---|---|
| `#prefab` | Output this node as a Prefab Asset |
| `#image` | Rasterize the whole frame as a single sprite (children are flattened into the render; no individual nodes are generated) |
| `#slice9` / `#sliced` | Import as a 9-sliced Image (requires a 3×3 grid of child elements + matching constraints on the Figma side) |
| `#container` | Treat as a plain container — use it when you want to skip smart-tag detection (e.g. auto button-ization) |
| `#button` | Add a UGUI `Button` |
| `#scroll` | Add a UGUI `ScrollRect` (see constraints below) |
| `#mask` | Add a UGUI `Mask` / `RectMask2D` |
| `#ignore` | Exclude this node and its descendants from import |
| `#use:<path-or-name>` | Instantiate an **existing project prefab** in place of this node instead of generating one from the Figma subtree (UGUI backend only) |

### Using `#use:` (reuse an existing prefab)

Bind a node to a prefab that already lives in your project — useful for hand-authored widgets you don't want regenerated from the design. The node's own Figma children are dropped; the referenced prefab is instantiated at the node's position.

```
SaveButton #use:Assets/UI/Prefabs/SaveButton.prefab   ← exact asset path
SaveButton #use:SaveButton                            ← bare name, resolved by project-wide search
```

- The reference is treated as a **path** when it contains `/` or ends in `.prefab` (a leading `Assets/` and the `.prefab` extension are added if missing); otherwise it's resolved by name via `AssetDatabase` (first prefab whose file name matches).
- Only **placement** (anchors / pivot / anchored position) from the Figma node is applied — the prefab keeps its own authored size.
- In **Diff Update**, an unchanged `#use:` node keeps its existing instance (and any manual overrides); it's only re-instantiated when the reference changes.
- If the prefab can't be resolved, the node is left as an empty placeholder and a message is logged.

### Using `#scroll` (current constraints)

The current implementation relies on **substring matching of child node names**. Build a structure like this on the Figma side:

```
ScrollFrame #scroll
└── Viewport               ← node name must contain "viewport" (case-insensitive)
    └── Content            ← node name must contain "content"
        ├── content item 1
        ├── content item 2
        └── ...
```

- If no child node name contains `viewport` / `content`, both `ScrollRect.viewport` and `ScrollRect.content` are assigned to the same RectTransform and **scrolling will not occur**.
- A mask is not added to the Viewport automatically. If you need clipping, also tag the Viewport node with `#mask`.
- On the Figma side, the content must be larger than the Viewport (an actual scrollable size difference). If both are the same size, the ScrollRect stays still.
- The scroll direction is inferred from Figma's `Overflow Direction` (defaults to vertical if unspecified).

Automatic Viewport / Content injection (working without a naming convention) is planned, but the naming above is required for now.

## 🔤 Fonts

**To render text exactly as it looks in Figma, you must provide a TMP Font Asset built from the same font used in Figma.** Without it, text is drawn with a fallback font (per the chain below), so glyph shapes, spacing, and weight will not match Figma.

### Resolution order

For each text node, the importer picks a font in this order:

1. **Exact match** — scans `TMP_FontAsset`s in the project and picks one whose `font.name` or `faceInfo.familyName` matches Figma's `fontFamily`, with matching weight and italic (highest score).
2. **Family + weight match** (italic difference allowed).
3. **Family name only** (including partial match).
4. If none of the above hit, the **Default Font** from Settings is used.
5. If the chosen font lacks glyphs for some characters, the **Fallback Fonts** list in Settings is tried in order.
6. If still unresolved and **Auto System Font Fallback (CJK)** is enabled, a TMP Font Asset is dynamically generated from the OS system fonts (CJK / symbols / emoji) and attached as a fallback.

### Creating a TMP Font Asset

1. Obtain the font file (`.ttf` / `.otf`) used in Figma.
2. Place it in any folder of your Unity project.
3. In `Window > TextMeshPro > Font Asset Creator`, select the font file and **Generate** with the character sets you need (Latin Extended, CJK Unified Ideographs, etc.).
4. Once the generated `TMP_FontAsset` is in the project, the importer picks it up automatically via the scoring in 1–3 above.
5. Matching accuracy improves if the `TMP_FontAsset` name or Face Info Family Name contains Figma's `fontFamily`.

If you use multiple weights / italics, provide a separate `TMP_FontAsset` for each (create Bold / Italic, etc. under the same family name).

### Related settings

In the Editor window's **Text & Fonts** section:

- **Default Font** — the default TMP Font Asset when no family-name match is found.
- **Fallback Fonts** — a list of additional TMP Font Assets to try when glyphs are missing.
- **Auto System Font Fallback (CJK)** — for strings containing CJK / symbols / emoji, dynamically generate and use a TMP Font Asset from the OS system fonts (enabled by default).

## 🛠️ Development

The `FigmaToUnity.Core` assembly is Unity-free (netstandard2.1) and compiles headlessly, so its logic is unit-tested outside Unity. Editor/Runtime code requires Unity and is verified by opening `LongGames > Figma Importer` in the Editor.

Requires the **.NET 10 SDK**.

```bash
# Headless compile check of Core
dotnet build FigmaToUnity/Core/FigmaToUnity.Core.csproj

# Run the unit tests
dotnet test FigmaToUnity.Core.Tests
```

## 📄 License

[MIT](LICENSE)
