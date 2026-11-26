using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore;

[CreateAssetMenu(fileName = "InputIconDatabase", menuName = "Input/Icon Database")]
public class InputIconDatabase : ScriptableObject
{
	[Serializable]
	public struct IconEntry
	{
		[Tooltip("unity internal control name")]
		public string controlPath;
		public Sprite sprite;
	}

	[Header("Auto Populate List")]
	[SerializeField] private InputActionAsset sourceAsset;
	
	[Header("Keyboard & Mouse")]
	[SerializeField] private List<IconEntry> keyboardMouseIcons = new();
	
	[Header("Gamepad")]
	[SerializeField] private List<IconEntry> gamepadIcons = new();
	
	
	[Space(20)]
	[Header("TMP Atlas (Automatically generated asset)")]
	[SerializeField] private TMP_SpriteAsset tmpSpriteAsset;
	
	public InputActionAsset SourceAsset => sourceAsset;
	public TMP_SpriteAsset TmpSpriteAsset => tmpSpriteAsset;

	private Dictionary<string, Sprite> keyboardLookup;
	private Dictionary<string, Sprite> gamepadLookup;


	private void OnEnable()
	{
		keyboardLookup = null;
		gamepadLookup = null;
	}


	
	private void OnValidate()
	{
		keyboardLookup = null;
		gamepadLookup = null;
	}
	
	

	public Sprite GetSprite(string controlPath, InputDeviceTracker.DeviceType deviceType)
	{
		BuildLookupsIfNeeded();

		string cleanName = ExtractControlName(controlPath);
		if (string.IsNullOrEmpty(cleanName)) return null;

		Dictionary<string, Sprite> primary = deviceType == InputDeviceTracker.DeviceType.Gamepad
			? gamepadLookup
			: keyboardLookup;

		if (primary.TryGetValue(cleanName, out Sprite sprite))
		{
			return sprite;
		}

		Dictionary<string, Sprite> secondary = deviceType == InputDeviceTracker.DeviceType.Gamepad
			? keyboardLookup
			: gamepadLookup;
		
		return secondary.TryGetValue(cleanName, out sprite) ? sprite : null;
	}



	private void BuildLookupsIfNeeded()
	{
		if (keyboardLookup != null && gamepadLookup != null) return;

		keyboardLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry entry in keyboardMouseIcons)
		{
			string key = ExtractControlName(entry.controlPath);
			if (!string.IsNullOrEmpty(key) && entry.sprite != null)
				keyboardLookup[key] = entry.sprite;
		}

		gamepadLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry entry in gamepadIcons)
		{
			string key = ExtractControlName(entry.controlPath);
			if (!string.IsNullOrEmpty(key) && entry.sprite != null)
				gamepadLookup[key] = entry.sprite;
		}
	}



	private static string ExtractControlName(string path)
	{
		if (string.IsNullOrEmpty(path)) return null;
		int slashIndex = path.LastIndexOf('/');
		return slashIndex >= 0 ? path.Substring(slashIndex + 1) : path;
	}



	[ContextMenu("Import Inputs from Source Asset")]
	private void PopulateFromAsset()
	{
		if (sourceAsset == null) return;
	
		HashSet<string> existingKb = new(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry e in keyboardMouseIcons) existingKb.Add(ExtractControlName(e.controlPath));
	
		HashSet<string> existingGp = new(StringComparer.OrdinalIgnoreCase);
		foreach (IconEntry e in gamepadIcons) existingGp.Add(ExtractControlName(e.controlPath));

		foreach (InputActionMap map in sourceAsset.actionMaps)
		{
			foreach (InputAction action in map.actions)
			{
				foreach (InputBinding binding in action.bindings)
				{
					if (binding.isComposite || string.IsNullOrEmpty(binding.path) || string.IsNullOrEmpty(binding.groups))
						continue;
				
					string cleanName = ExtractControlName(binding.path);
					bool isGamepad = binding.groups.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
					bool isKeyboardMouse = binding.groups.IndexOf("Keyboard&Mouse", StringComparison.OrdinalIgnoreCase) >= 0;

					if (isGamepad && existingGp.Add(cleanName))
					{
						gamepadIcons.Add(new IconEntry { controlPath = binding.path });
					}
					else if (isKeyboardMouse && existingKb.Add(cleanName))
					{
						keyboardMouseIcons.Add(new IconEntry { controlPath = binding.path });
					}
				}
			}
		}
	
#if UNITY_EDITOR
		UnityEditor.EditorUtility.SetDirty(this);
#endif
	}
	
	
	#if UNITY_EDITOR
[ContextMenu("Generate TMP Sprite Asset")]
private void GenerateTmpSpriteAsset()
{
	HashSet<Sprite> uniqueSprites = new();
	foreach (IconEntry e in keyboardMouseIcons)
		if (e.sprite != null) uniqueSprites.Add(e.sprite);
	foreach (IconEntry e in gamepadIcons)
		if (e.sprite != null) uniqueSprites.Add(e.sprite);

	if (uniqueSprites.Count == 0)
	{
		Debug.LogWarning("[IconDB] No assigned sprite in DB.");
		return;
	}

	List<Sprite> spriteList = new(uniqueSprites);
	Texture2D[] readableTextures = new Texture2D[spriteList.Count];

	for (int i = 0; i < spriteList.Count; i++)
	{
		readableTextures[i] = ExtractReadableSprite(spriteList[i]);
	}

	Texture2D atlas = new(2048, 2048, TextureFormat.RGBA32, false);
	Rect[] rects = atlas.PackTextures(readableTextures, 4, 2048, false);

	foreach (Texture2D tex in readableTextures) DestroyImmediate(tex);

	string dbPath = UnityEditor.AssetDatabase.GetAssetPath(this);
	string folder = Path.GetDirectoryName(dbPath);
	string pngPath = Path.Combine(folder!, $"{name}_Atlas.png").Replace('\\', '/');
	string assetPath = Path.Combine(folder, $"{name}_TMPSpriteAsset.asset").Replace('\\', '/');

	File.WriteAllBytes(pngPath, atlas.EncodeToPNG());
	DestroyImmediate(atlas);
	UnityEditor.AssetDatabase.ImportAsset(pngPath);

	UnityEditor.TextureImporter importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(pngPath);
	importer.textureType = UnityEditor.TextureImporterType.Sprite;
	importer.alphaIsTransparency = true;
	importer.mipmapEnabled = false;
	importer.SaveAndReimport();

	Texture2D savedAtlas = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

	TMP_SpriteAsset spriteAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
	bool isNew = spriteAsset == null;
	if (isNew)
	{
		spriteAsset = CreateInstance<TMP_SpriteAsset>();
		spriteAsset.name = $"{name}_TMPSpriteAsset";
	}

	spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(spriteAsset.name);
	spriteAsset.spriteSheet = savedAtlas;
	spriteAsset.spriteGlyphTable.Clear();
	spriteAsset.spriteCharacterTable.Clear();

	for (int i = 0; i < spriteList.Count; i++)
	{
		Rect r = rects[i];
		int x = Mathf.RoundToInt(r.x * savedAtlas.width);
		int y = Mathf.RoundToInt(r.y * savedAtlas.height);
		int w = Mathf.RoundToInt(r.width * savedAtlas.width);
		int h = Mathf.RoundToInt(r.height * savedAtlas.height);

		TMP_SpriteGlyph glyph = new()
		{
			index = (uint)i,
			metrics = new GlyphMetrics(w, h, 0f, h * 0.8f, w),
			glyphRect = new GlyphRect(x, y, w, h),
			scale = 1f,
			atlasIndex = 0
		};
		spriteAsset.spriteGlyphTable.Add(glyph);

		TMP_SpriteCharacter character = new(0xFFFE, spriteAsset, glyph)
		{
			name = spriteList[i].name,
			scale = 1f
		};
		spriteAsset.spriteCharacterTable.Add(character);
	}

	if (isNew)
	{
		Material mat = new(Shader.Find("TextMeshPro/Sprite")) { name = $"{spriteAsset.name} Material" };
		mat.SetTexture(ShaderUtilities.ID_MainTex, savedAtlas);
		spriteAsset.material = mat;

		UnityEditor.AssetDatabase.CreateAsset(spriteAsset, assetPath);
		UnityEditor.AssetDatabase.AddObjectToAsset(mat, spriteAsset);
	}
	else
	{
		spriteAsset.material.SetTexture(ShaderUtilities.ID_MainTex, savedAtlas);
	}
	
	tmpSpriteAsset = spriteAsset;
	if (TMP_Settings.defaultSpriteAsset == null)
	{
		TMP_Settings.defaultSpriteAsset = spriteAsset;
		UnityEditor.EditorUtility.SetDirty(TMP_Settings.instance);
	}

	UnityEditor.EditorUtility.SetDirty(this);
	UnityEditor.EditorUtility.SetDirty(spriteAsset);
	UnityEditor.AssetDatabase.SaveAssets();

	spriteAsset.UpdateLookupTables();
	Debug.Log($"[IconDB] TMP_SpriteAsset generated with {spriteList.Count} icons : {assetPath}");
}

private static Texture2D ExtractReadableSprite(Sprite sprite)
{
	Rect r = sprite.textureRect;
	int w = Mathf.RoundToInt(r.width);
	int h = Mathf.RoundToInt(r.height);

	RenderTexture rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
	Graphics.Blit(sprite.texture, rt);

	RenderTexture previous = RenderTexture.active;
	RenderTexture.active = rt;

	Texture2D copy = new(w, h, TextureFormat.RGBA32, false);
	copy.ReadPixels(new Rect(r.x, sprite.texture.height - r.y - r.height, w, h), 0, 0);
	copy.Apply();

	RenderTexture.active = previous;
	RenderTexture.ReleaseTemporary(rt);
	return copy;
}
#endif
}
