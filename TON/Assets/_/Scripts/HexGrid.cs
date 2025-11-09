using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class HexGrid : MonoBehaviour
{
    public int size = 6;
	public HexCell cellPrefab;
    public Text cellLabelPrefab;
    public GameObject borderPrefab;
    public GameObject floorPrefab;

    Init init;
    public const float outerRadius = 6.06f;
    public const float innerRadius = outerRadius * 0.866025404f;

    List<HexCell> cells = new List<HexCell>();
    Canvas gridCanvas;

    void Awake()
    {
        gridCanvas = GetComponentInChildren<Canvas>();
        for (int z = -size; z <= size; z++)
        {
            for (int x = -size; x <= size; x++)
            {
                CreateCell(x, z);
            }
        }
        GetRooms();
        //StartCoroutine(Routine());
    }

    private void Start()
    {
        init = GameObject.Find("Init").GetComponent<Init>();
    }

    IEnumerator Routine()
    {
        GetRooms();
        yield return new WaitForSeconds(10);
        Debug.Log("complete");
        StartCoroutine(Routine());
    }

    void CreateCell(int x, int z)
    {
        var c = HexCoordinates.FromOffsetCoordinates(x, z);
        if (c.x > size || c.x < -size) return;
        if (c.y > size || c.y < -size) return;
        if (c.z > size || c.z < -size) return;
        if (c.x < 4 && c.x > -4 && c.y < 4 && c.y > -4 && c.z < 4 && c.z > -4) return;

        Vector3 position;
        position.x = (x + z * 0.5f - z / 2) * (innerRadius * 2f);
        position.y = -2.5f;
        position.z = z * (outerRadius * 1.5f);

        var cell = Instantiate<HexCell>(cellPrefab);
        cell.transform.SetParent(transform, false);
        cell.transform.localPosition = position;
        cell.coordinates = c;
        if (c.z % 2 == 0) { cell.transform.Rotate(new Vector3(0, 180, 0)); cell.rotate = true; }
        var text = cell.transform.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.SetText(c.ToStringOnSeparateLines());
        cells.Add(cell);

        var label = Instantiate<Text>(cellLabelPrefab);
        label.rectTransform.SetParent(gridCanvas.transform, false);
        label.rectTransform.anchoredPosition = new Vector2(position.x, position.z);
        label.text = cell.coordinates.ToStringOnSeparateLines();

        /*var room = Instantiate<GameObject>(roomPrefab);
        room.transform.SetParent(transform, false);
        room.transform.localPosition = position;*/
    }

    #region nft
    static readonly string[] CacheExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".bmp" };

    class PermissiveCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }

    IEnumerator DownloadImage(string url, Image image, System.Action a = null, bool calc = false, RectTransform t = null, HexCell cell = null, List<Transform> walls = null)
    {
        string originalUrl = url;
        string normalizedUrl = NormalizeUrl(url);
        string mimeFromDataUri = null;
        bool isDataUri = IsDataUri(normalizedUrl);
        string extension = ".png";
        if (isDataUri)
        {
            mimeFromDataUri = ExtractMimeTypeFromDataUri(normalizedUrl);
            extension = ExtensionFromMime(mimeFromDataUri) ?? extension;
        }
        else
        {
            extension = DetermineExtension(normalizedUrl) ?? extension;
        }

        string cachePath = ResolveCachePath(originalUrl, extension);
        if (!string.IsNullOrEmpty(cachePath) && !File.Exists(cachePath))
        {
            foreach (var candidate in CacheExtensions)
            {
                if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)) continue;
                string altPath = ResolveCachePath(originalUrl, candidate);
                if (!string.IsNullOrEmpty(altPath) && File.Exists(altPath))
                {
                    cachePath = altPath;
                    extension = candidate;
                    break;
                }
            }
        }
        byte[] rawData = null;
        Sprite sprite = null;
        Texture2D texture = null;

        if (!string.IsNullOrEmpty(cachePath))
        {
            try
            {
                if (File.Exists(cachePath))
                {
                    if (string.Equals(Path.GetExtension(cachePath), ".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        string svgText = File.ReadAllText(cachePath, Encoding.UTF8);
                        sprite = BuildSpriteFromSvg(svgText, out Vector2 svgSize);
                        texture = null;
                        rawData = Encoding.UTF8.GetBytes(svgText);
                        if (sprite != null)
                        {
                            ApplySprite(image, sprite, texture, a, t, cell, calc, walls, originalUrl, svgSize.x, svgSize.y);
                            yield break;
                        }
                    }
                    else
                    {
                        rawData = File.ReadAllBytes(cachePath);
                        if (TryCreateSprite(rawData, out texture, out sprite))
                        {
                            ApplySprite(image, sprite, texture, a, t, cell, calc, walls, originalUrl, texture.width, texture.height);
                            yield break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load cached NFT image '{cachePath}': {ex.Message}");
            }
        }

        if (isDataUri)
        {
            if (TryDecodeDataUri(normalizedUrl, out rawData))
            {
                if (string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) || (mimeFromDataUri != null && mimeFromDataUri.Contains("svg")))
                {
                    string svgText = Encoding.UTF8.GetString(rawData);
                    sprite = BuildSpriteFromSvg(svgText, out Vector2 svgSize);
                    if (sprite != null)
                    {
                        string svgCachePath = ResolveCachePath(originalUrl, ".svg");
                        TryCacheSvg(svgCachePath, svgText);
                        ApplySprite(image, sprite, null, a, t, cell, calc, walls, originalUrl, svgSize.x, svgSize.y);
                        yield break;
                    }
                }
                else if (TryCreateSprite(rawData, out texture, out sprite))
                {
                    string cacheTarget = ResolveCachePath(originalUrl, extension);
                    TryCacheBytes(cacheTarget, rawData);
                    ApplySprite(image, sprite, texture, a, t, cell, calc, walls, originalUrl, texture.width, texture.height);
                    yield break;
                }
            }
            Debug.LogWarning($"Unable to decode NFT data URI for '{originalUrl}'.");
            yield break;
        }

        yield return TryDownloadSprite(normalizedUrl, extension, cachePath, originalUrl, image, a, calc, t, cell, walls);
    }

    IEnumerator TryDownloadSprite(string normalizedUrl, string extension, string cachePath, string originalUrl, Image image, System.Action a, bool calc, RectTransform t, HexCell cell, List<Transform> walls)
    {
        Sprite sprite = null;
        Texture2D texture = null;
        Vector2 svgSize = Vector2.zero;
        byte[] data = null;
        string finalUrl = normalizedUrl;
        bool attemptedHttpsFallback = false;

        while (true)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(finalUrl))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 30;
                if (finalUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    request.certificateHandler = new PermissiveCertificateHandler();
                }
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (!attemptedHttpsFallback && finalUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        finalUrl = "https://" + finalUrl.Substring(7);
                        attemptedHttpsFallback = true;
                        continue;
                    }
                    Debug.LogWarning($"Failed to download NFT image '{originalUrl}' from '{finalUrl}': {request.error}");
                    yield break;
                }

                data = request.downloadHandler.data;
                string contentType = request.GetResponseHeader("Content-Type");
                string inferredExtension = DetermineExtensionFromResponse(extension, contentType, finalUrl);
                string resolvedCachePath = cachePath;
                if (!string.IsNullOrEmpty(inferredExtension))
                {
                    resolvedCachePath = ResolveCachePath(originalUrl, inferredExtension);
                }
                bool isSvg = IsSvgContent(inferredExtension, contentType, finalUrl);

                if (isSvg)
                {
                    string svgText = Encoding.UTF8.GetString(data, 0, data.Length);
                    sprite = BuildSpriteFromSvg(svgText, out svgSize);
                    if (sprite != null)
                    {
                        TryCacheSvg(resolvedCachePath, svgText);
                        ApplySprite(image, sprite, null, a, t, cell, calc, walls, originalUrl, svgSize.x, svgSize.y);
                    }
                    else
                    {
                        Debug.LogWarning($"Unable to tessellate SVG NFT '{originalUrl}'.");
                    }
                }
                else if (TryCreateSprite(data, out texture, out sprite))
                {
                    TryCacheBytes(resolvedCachePath, data);
                    ApplySprite(image, sprite, texture, a, t, cell, calc, walls, originalUrl, texture.width, texture.height);
                }
                else
                {
                    yield return TryDownloadWithTextureHandler(finalUrl, resolvedCachePath, originalUrl, image, a, calc, t, cell, walls);
                }
                yield break;
            }
        }
    }

    IEnumerator TryDownloadWithTextureHandler(string url, string cachePath, string originalUrl, Image image, System.Action a, bool calc, RectTransform t, HexCell cell, List<Transform> walls)
    {
        using (UnityWebRequest textureRequest = UnityWebRequestTexture.GetTexture(url))
        {
            textureRequest.timeout = 30;
            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                textureRequest.certificateHandler = new PermissiveCertificateHandler();
            }
            yield return textureRequest.SendWebRequest();
            if (textureRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Failed to decode NFT image '{originalUrl}' with texture handler: {textureRequest.error}");
                yield break;
            }

            var handlerTexture = (DownloadHandlerTexture)textureRequest.downloadHandler;
            var tex = handlerTexture.texture;
            if (tex == null)
            {
                Debug.LogWarning($"NFT image '{originalUrl}' returned no texture data.");
                yield break;
            }

            byte[] pngData = null;
            try
            {
                pngData = tex.EncodeToPNG();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to encode NFT texture '{originalUrl}' to PNG: {ex.Message}");
            }

            if (pngData != null && pngData.Length > 0)
            {
                string pngCachePath = ResolveCachePath(originalUrl, ".png");
                TryCacheBytes(pngCachePath, pngData);
            }
            else
            {
                TryCacheBytes(cachePath, textureRequest.downloadHandler.data);
            }

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            ApplySprite(image, sprite, tex, a, t, cell, calc, walls, originalUrl, tex.width, tex.height);
        }
    }

    static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        string trimmed = url.Trim();
        if (trimmed.StartsWith("ipfs://", StringComparison.OrdinalIgnoreCase))
        {
            string path = trimmed.Substring(7);
            path = path.TrimStart('/');
            if (path.StartsWith("ipfs/", StringComparison.OrdinalIgnoreCase)) path = path.Substring(5);
            return $"https://cloudflare-ipfs.com/ipfs/{path}";
        }
        if (trimmed.StartsWith("tonstorage://", StringComparison.OrdinalIgnoreCase))
        {
            string path = trimmed.Substring("tonstorage://".Length);
            path = path.TrimStart('/');
            return $"https://tonstorage.net/{path}";
        }
        if (trimmed.StartsWith("//")) return "https:" + trimmed;
        if (trimmed.StartsWith("http://45.132.107.107", StringComparison.OrdinalIgnoreCase))
        {
            return "https://rooms.worldofton.ru" + trimmed.Substring("http://45.132.107.107".Length);
        }
        return trimmed;
    }

    static bool IsDataUri(string url)
    {
        return url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    static string ExtractMimeTypeFromDataUri(string dataUri)
    {
        var match = Regex.Match(dataUri, "^data:(?<mime>[^;]+);", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["mime"].Value : null;
    }

    static bool TryDecodeDataUri(string dataUri, out byte[] data)
    {
        data = null;
        var match = Regex.Match(dataUri, "^data:[^;]+;base64,(?<data>.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        try
        {
            data = Convert.FromBase64String(match.Groups["data"].Value);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to decode NFT data URI: {ex.Message}");
            return false;
        }
    }

    static string DetermineExtension(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                string ext = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrEmpty(ext)) return ext.ToLowerInvariant();
            }
        }
        catch { }

        var match = Regex.Match(url, @"\.([A-Za-z0-9]+)(?:\?|$)");
        if (match.Success) return "." + match.Groups[1].Value.ToLowerInvariant();
        return null;
    }

    static string DetermineExtensionFromResponse(string fallback, string contentType, string url)
    {
        if (!string.IsNullOrEmpty(contentType))
        {
            string byMime = ExtensionFromMime(contentType);
            if (!string.IsNullOrEmpty(byMime)) return byMime;
        }
        return DetermineExtension(url) ?? fallback;
    }

    static string ExtensionFromMime(string mime)
    {
        if (string.IsNullOrEmpty(mime)) return null;
        mime = mime.Split(';')[0].Trim().ToLowerInvariant();
        switch (mime)
        {
            case "image/jpeg":
            case "image/jpg":
                return ".jpg";
            case "image/png":
                return ".png";
            case "image/gif":
                return ".gif";
            case "image/svg+xml":
                return ".svg";
            case "image/webp":
                return ".webp";
            case "image/bmp":
                return ".bmp";
            default:
                return null;
        }
    }

    static bool IsSvgContent(string extension, string contentType, string url)
    {
        if (!string.IsNullOrEmpty(contentType) && contentType.IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (!string.IsNullOrEmpty(extension) && extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)) return true;
        return url.IndexOf(".svg", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string ResolveCachePath(string url, string extension)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "nft-cache");
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            if (string.IsNullOrEmpty(extension)) extension = ".png";
            if (!extension.StartsWith(".")) extension = "." + extension;

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return Path.Combine(directory, sb.ToString() + extension);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to prepare NFT cache path for '{url}': {ex.Message}");
            return null;
        }
    }

    static bool TryCreateSprite(byte[] data, out Texture2D texture, out Sprite sprite)
    {
        texture = null;
        sprite = null;
        if (data == null || data.Length == 0) return false;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, data))
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
                return false;
            }
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to build texture from NFT image: {ex.Message}");
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
            sprite = null;
            return false;
        }
    }

    static Sprite BuildSpriteFromSvg(string svgText, out Vector2 size)
    {
        size = Vector2.zero;
        if (string.IsNullOrEmpty(svgText)) return null;
        try
        {
            var tessOptions = new VectorUtils.TessellationOptions()
            {
                StepDistance = 100.0f,
                MaxCordDeviation = 0.5f,
                MaxTanAngleDeviation = 0.1f,
                SamplingStepSize = 0.01f
            };
            var sceneInfo = SVGParser.ImportSVG(new StringReader(svgText));
            var geoms = VectorUtils.TessellateScene(sceneInfo.Scene, tessOptions);
            var sprite = VectorUtils.BuildSprite(geoms, 10.0f, VectorUtils.Alignment.Center, Vector2.zero, 128, true);
            size = sceneInfo.SceneViewport.size;
            if (size == Vector2.zero)
            {
                size = new Vector2(sprite.rect.width, sprite.rect.height);
            }
            return sprite;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to parse SVG NFT: {ex.Message}");
            return null;
        }
    }

    void ApplySprite(Image image, Sprite sprite, Texture2D texture, System.Action a, RectTransform t, HexCell cell, bool calc, List<Transform> walls, string originalUrl, float width, float height)
    {
        if (image == null || sprite == null) return;
        image.preserveAspect = true;
        image.sprite = sprite;

        a?.Invoke();

        if (t != null)
        {
            if (width <= 0 || height <= 0)
            {
                if (texture != null)
                {
                    width = texture.width;
                    height = texture.height;
                }
                else
                {
                    width = sprite.rect.width;
                    height = sprite.rect.height;
                }
            }
            CalcSize(width, height, t, cell, calc, walls, originalUrl);
        }
    }

    static void TryCacheBytes(string cachePath, byte[] data)
    {
        if (string.IsNullOrEmpty(cachePath) || data == null || data.Length == 0) return;
        try
        {
            File.WriteAllBytes(cachePath, data);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to cache NFT image to '{cachePath}': {ex.Message}");
        }
    }

    static void TryCacheSvg(string cachePath, string svgText)
    {
        if (string.IsNullOrEmpty(cachePath) || string.IsNullOrEmpty(svgText)) return;
        try
        {
            File.WriteAllText(cachePath, svgText, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to cache SVG NFT to '{cachePath}': {ex.Message}");
        }
    }

    IEnumerator DownloadSVG(string url, SVGImage image, RectTransform t, HexCell cell)
    {
        UnityWebRequest www = UnityWebRequest.Get(url);

        yield return www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success) Debug.Log(www.error);
        else
        {
            //Convert byte[] data of svg into string
            string bitString = System.Text.Encoding.UTF8.GetString(www.downloadHandler.data);
            var tessOptions = new VectorUtils.TessellationOptions()
            {
                StepDistance = 100.0f,
                MaxCordDeviation = 0.5f,
                MaxTanAngleDeviation = 0.1f,
                SamplingStepSize = 0.01f
            };

            // Dynamically import the SVG data, and tessellate the resulting vector scene.
            var sceneInfo = SVGParser.ImportSVG(new System.IO.StringReader(bitString));
            var geoms = VectorUtils.TessellateScene(sceneInfo.Scene, tessOptions);

            // Build a sprite with the tessellated geometry
            Sprite sprite = VectorUtils.BuildSprite(geoms, 10.0f, VectorUtils.Alignment.Center, Vector2.zero, 128, true);
            image.sprite = sprite;

            CalcSize(1000, 1000, t, cell, true, null, null);
        }
    }

    async void CalcSize(float width, float height, RectTransform t, HexCell cell, bool calc, List<Transform> walls, string url)
    {
        //Øèðèíà è âûñîòà êàðòèíêè
        var w = width / 1000;
        var h = height / 1000;
        if (w < 1) { var delta = w / h; w = 1; h = w / delta; }
        if (h < 1) { var delta = h / w; h = 1; w = h / delta; }
        t.sizeDelta = new Vector2(w, h);

        w += 0.1f;  //ïðîñòðàíñòâî ìåæäó êàðòèíêàìè
        var w1 = w; //øèðèíà ñîõðàíÿåòñÿ äëÿ ïåðâîé êàðòèíêè íîâîãî ðÿäà
        var sum = cell.w.Sum(); //ñóììà øèðèíû âñåõ êàðòèíîê ðÿäà
        cell.w.Add(w);  //øèðèíà ñîõðàíÿåòñÿ â îáùåì ñïèñêå
        w /= 2; //øèðèíà äåëèòñÿ íà 2 èç-çà ñäâèãà êîîðäèíàò
        w += sum;   //ïðèáàâëÿåòñÿ ê îáùåé ñóììå
        if (w + w1 > 5.8f)  //åñëè êàðòèíêà íå âìåùàåòñÿ â øèðèíó õîëñòà
        {
            cell.w.Clear(); //î÷èùàåòñÿ ñïèñîê øèðèíû
            cell.w.Add(w1); //äîáàâëÿåòñÿ íà÷àëüíàÿ øèðèíà êàðòèíêè
            w = w1 / 2; //äåëèòñÿ íà 2, ÷òîáû íå âûëàçèëà çà ïðåäåëû ñëåâà
            cell.maxHeight += cell.h.Max() + 0.1f;  //ñîõðàíÿåòñÿ ìàêñèìàëüíàÿ âûñîòà ðÿäà
            cell.h.Clear(); //î÷èùàåòñÿ ñïèñîê âûñîò
        }
        cell.h.Add(h);  //âûñîòà äîáàâëÿåòñÿ â îáùèé ñïèñîê
        var h1 = h;
        h = -h / 2 - cell.maxHeight;    //âûñîòà âû÷èñëÿåòñÿ ñäâèãîì âíèç, äåëåíèåì íà 2 è ïðèáàâëåíèåì ìàêñèìàëüíîé âûñîòû âåðõíèõ ðÿäîâ
        if (h - h1 < -4.8f)
        {
            cell.index++;
            cell.h.Clear();
            cell.w.Clear();
            w = w1 / 2;
            cell.maxHeight = 0;
            h = -h1 / 2;
        }
        if (calc)
        {
            if (cell.index < walls.Count)
            {
                t.transform.SetParent(walls[cell.index]);
                t.localPosition = new Vector3(w, h, 0);
                t.anchoredPosition = new Vector2(w, h);
                t.localEulerAngles = Vector3.zero;
                await Helper.Post("http://45.132.107.107/index/SetPosition", $"address={cell.coordinates.address}&image={url}&x={w.ToString().Replace(",", ".")}&y={h.ToString().Replace(",", ".")}&scale={1}&index={cell.index}");
            }
        }
    }

    public async void Auth()
    {
        init.ButtonAuth.SetActive(false);
        string session = Helper.RandomString();
        Application.OpenURL($"http://45.132.107.107/index/connect?session={session}");

        while (true)
        {
            await Task.Delay(100);
            var j = await Helper.Post("http://45.132.107.107/index/GetAddress", $"session={session}&x={init.Player.coordinates.x}&y={init.Player.coordinates.y}&z={init.Player.coordinates.z}");
            if (j != null && j["address"] != null)
            {
                string address = j["address"].ToString();
                if (!string.IsNullOrEmpty(address))
                {
                    PlayerPrefs.SetString("address", address);
                    init.ButtonNftCollection.SetActive(true);
                    break;
                }
            }
        }
    }

    public async Task GetNft()
    {
        Font arial = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        Helper.ClearChildren(init.NFTCollection.transform);
        init.OpenNftCollection();
        string address = PlayerPrefs.GetString("address");
        var j = await Helper.Post("http://45.132.107.107/index/GetNft", $"address={address}");
        foreach (var item in j["images"])
        {
            var panel = new GameObject("Panel");
            panel.AddComponent<CanvasRenderer>();
            var panel_t = panel.AddComponent<RectTransform>();
            panel_t.transform.SetParent(init.NFTCollection.transform);
            panel_t.sizeDelta = new Vector2(300, 50);
            panel_t.anchoredPosition = new Vector2(0, 0);
            panel_t.localPosition = new Vector2(0, 0);
            var si = panel.AddComponent<SelectImage>();
            si.url = item["url"].ToString();
            si.selected = (bool)item["selected"];
            si.description = item["description"].ToString();
            si.link = item["externalUrl"].ToString();
            var panel_image = panel.AddComponent<Image>();
            if (si.selected) panel_image.color = new Color32(0, 0, 0, 100);
            else panel_image.color = new Color32(0, 0, 0, 30);

            var img_o = new GameObject("nft");
            var t = img_o.AddComponent<RectTransform>();
            t.transform.SetParent(panel.transform);
            t.sizeDelta = new Vector2(30, 30);
            t.localPosition = Vector3.zero;
            t.anchoredPosition = new Vector2(25, 0);
            t.anchorMin = new Vector2(0, 0.5f);
            t.anchorMax = new Vector2(0, 0.5f);
            var image = img_o.AddComponent<Image>();
            StartCoroutine(DownloadImage(si.url, image, () => { t.sizeDelta = Helper.ImageRatio(image, 30); }));

            var text_o = new GameObject("text");
            var text_t = text_o.AddComponent<RectTransform>();
            text_t.transform.SetParent(panel.transform);
            text_t.sizeDelta = new Vector2(250, 50);
            text_t.localPosition = Vector3.zero;
            text_t.anchoredPosition = new Vector2(175, 0);
            text_t.anchorMin = new Vector2(0, 0.5f);
            text_t.anchorMax = new Vector2(0, 0.5f);
            var text = text_o.AddComponent<Text>();
            text.text = item["name"].ToString();
            text.font = arial;
            text.alignment = TextAnchor.MiddleLeft;

            var border = Instantiate<GameObject>(borderPrefab);
            border.transform.SetParent(init.NFTCollection.transform);
        }
    }

    public async void GetNftButton()
    {
        await GetNft();
    }

    void AddNft(string url, HexCell cell, float ix, float iy, float iscale, int index, int floor)
    {
        var walls = new List<Transform>();
        var walls_o = cell.transform.Find("Walls");
        if (floor > 0 && cell.floors >= floor) walls_o = cell.transform.Find($"Floor_{floor}").Find("Walls");
        foreach (Transform tr in walls_o.transform) if (tr.tag == "wall") walls.Add(tr);
        var img_o = new GameObject("nft");
        var t = img_o.AddComponent<RectTransform>();
        t.transform.SetParent(walls[index]);
        bool calc = false;
        t.localEulerAngles = Vector3.zero;
        t.sizeDelta = new Vector2(0.1f, 0.1f);
        t.anchorMin = new Vector2(0, 1);
        t.anchorMax = new Vector2(0, 1);
        if (ix == 0 && iy == 0 && iscale == 0)
        {
            calc = true;
            t.localScale = Vector3.one;
            t.localPosition = Vector3.zero;
            t.anchoredPosition = Vector2.zero;
        }
        else
        {
            t.localScale = new Vector3(iscale, iscale, 0);
            t.localPosition = new Vector3(ix, iy, 0);
            t.anchoredPosition = new Vector2(ix, iy);
        }
        if (cell.coordinates.address == PlayerPrefs.GetString("address"))
        {
            var di = img_o.AddComponent<DragImage>();
            di.url = url;
            di.index = index;
            di.walls = walls;
        }

        var image = img_o.AddComponent<Image>();
        StartCoroutine(DownloadImage(url, image, null, calc, t, cell, walls));
    }

    //TODO: îïòèìèçèðîâàòü
    public async void GetRooms()
    {
        var j = await Helper.Post("http://45.132.107.107/index/GetRooms", $"");
        foreach (var item in j["rooms"])
        {
            int x = (int)item["x"];
            int y = (int)item["y"];
            int z = (int)item["z"];
            string address = item["address"].ToString();
            var cell = cells.FirstOrDefault(q => q.coordinates.x == x && q.coordinates.y == y && q.coordinates.z == z);
            ClearRoom(cell);
            cell.coordinates.address = address;

            //Ýòàæè
            int floors = (int)item["floors"];
            if (floors > 0)
            {
                float h = 0.5f;
                for (int i = 1; i <= floors; i++)
                {
                    h += 5;
                    var floor = Instantiate<GameObject>(floorPrefab);
                    floor.name = $"Floor_{i}";
                    floor.tag = "floor";
                    floor.transform.SetParent(cell.transform, false);
                    floor.transform.localPosition = new Vector3(0, h, 0);
                    if (floors > i)
                    {
                        floor.transform.Find("Lift").gameObject.SetActive(true);
                        floor.transform.Find("Platform").gameObject.SetActive(false);
                    }
                }
                cell.transform.Find("Lift").gameObject.SetActive(true);
                cell.transform.Find("Platform").localPosition = new Vector3(21.4419994f, 0.4f, 28.9950008f);
                if (cell.coordinates.address == PlayerPrefs.GetString("address")) init.Player.floors = floors;
                cell.floors = floors;
                cell.GetComponentInChildren<Lift>().StartLift();
            }

            var split = item["images"].ToString().Split('|');
            int id = (int)item["id"];
            foreach (var image in j["images"])
            {
                int roomId = (int)image["roomId"];
                if (roomId == id)
                {
                    string url = image["url"].ToString();
                    float ix = (float)image["x"];
                    float iy = (float)image["y"];
                    float iscale = (float)image["scale"];
                    int index = (int)image["wall"];
                    int floor = (int)image["floor"];
                    if (!string.IsNullOrEmpty(url)) AddNft(url, cell, ix, iy, iscale, index, floor);
                }
            }
        }

        foreach (var cell in cells)
        {
            if (cell.floors > 0)
            {
                for (int i = 1; i <= 4; i++)
                {
                    int x = 0, y = 0, z = 0;
                    if (i == 1) { x = cell.rotate ? 0 : 0; y = cell.rotate ? 1 : -1; z = cell.rotate ? -1 : 1; }
                    if (i == 2) { x = cell.rotate ? -1 : -1; y = cell.rotate ? 1 : -1; z = cell.rotate ? 0 : 0; }
                    if (i == 3) { x = cell.rotate ? -1 : 1; y = cell.rotate ? 0 : 0; z = cell.rotate ? 1 : -1; }
                    if (i == 4) { x = cell.rotate ? 1 : -1; y = cell.rotate ? -1 : 1; z = cell.rotate ? 0 : 0; }
                    x += cell.coordinates.x; y += cell.coordinates.y; z += cell.coordinates.z;
                    /*if (cell.coordinates.x == -2 && cell.coordinates.y == -2 && cell.coordinates.z == 4 && i == 2) 
                        Debug.Log($"{x};{y};{z}");*/
                    var c = cells.FirstOrDefault(q => q.coordinates.x == x && q.coordinates.y == y && q.coordinates.z == z);
                    if (c != null && (c.floors >= cell.floors || cell.floors >= c.floors))
                    {
                        int floor = cell.floors;
                        if (c.floors < floor) floor = c.floors;
                        for (int k = 1; k <= floor; k++)
                        {
                            cell.transform.Find($"Floor_{k}").Find($"Window_{i}").gameObject.SetActive(false);
                            cell.transform.Find($"Floor_{k}").Find($"Door_{i}").gameObject.SetActive(true);
                        }
                    }
                }
            }
        }
    }

    void ClearRoom(HexCell cell)
    {
        cell.coordinates.address = null;
        cell.maxHeight = 0;
        cell.w.Clear();
        cell.h.Clear();
        var walls_o = cell.transform.Find("Walls");
        foreach (Transform tr in walls_o.transform) if (tr.tag == "wall") Helper.ClearChildren(tr.transform);
        foreach (Transform tr in cell.transform) if (tr.tag == "floor") { tr.name = "destroy"; GameObject.Destroy(tr.gameObject); }
    }
    #endregion
}
