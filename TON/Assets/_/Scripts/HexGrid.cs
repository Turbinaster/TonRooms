using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
    IEnumerator DownloadImage(string url, Image image, System.Action a = null, bool calc = false, RectTransform t = null, HexCell cell = null, List<Transform> walls = null)
    {
        if (image == null)
        {
            yield break;
        }

        string originalUrl = url;
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in BuildUrlCandidates(url))
        {
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            if (!attempted.Add(candidate))
            {
                continue;
            }

            bool succeeded = false;
            yield return DownloadImageAttempt(candidate, originalUrl, image, a, calc, t, cell, walls, success => succeeded = success);
            if (succeeded)
            {
                yield break;
            }
        }

        Debug.LogError($"[HexGrid] DownloadImage exhausted {attempted.Count} url variants for original='{originalUrl}'");
    }

    private IEnumerable<string> BuildUrlCandidates(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            yield return url;
        }

        var resolved = ResolveNftUrl(url);
        if (!string.IsNullOrEmpty(resolved) && resolved != url)
        {
            Debug.Log($"[HexGrid] DownloadImage normalized url from '{url}' to '{resolved}'");
            yield return resolved;
        }

        var baseForParsing = string.IsNullOrEmpty(resolved) ? url : resolved;
        if (Uri.TryCreate(baseForParsing, UriKind.Absolute, out var uri))
        {
            var absoluteEscaped = uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            if (!string.IsNullOrEmpty(absoluteEscaped))
            {
                yield return absoluteEscaped;
            }

            var schemeAndPath = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
            var normalizedQuery = NormalizeQuery(uri.Query);
            if (!string.IsNullOrEmpty(schemeAndPath))
            {
                yield return string.IsNullOrEmpty(normalizedQuery) ? schemeAndPath : $"{schemeAndPath}?{normalizedQuery}";
            }

            var escapeUriString = Uri.EscapeUriString(uri.ToString());
            if (!string.IsNullOrEmpty(escapeUriString))
            {
                yield return escapeUriString;
            }

            if (!string.IsNullOrEmpty(baseForParsing) && baseForParsing.Contains(" "))
            {
                yield return baseForParsing.Replace(" ", "%20");
            }
        }
        else if (!string.IsNullOrEmpty(baseForParsing) && baseForParsing.Contains(" "))
        {
            yield return baseForParsing.Replace(" ", "%20");
        }
    }

    private string NormalizeQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }

        if (query.StartsWith("?"))
        {
            query = query.Substring(1);
        }

        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var segments = query.Split('&');
        bool first = true;
        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            if (!first)
            {
                builder.Append('&');
            }
            first = false;

            var kvp = segment.Split(new[] { '=' }, 2);
            string key = kvp.Length > 0 ? Uri.EscapeDataString(Uri.UnescapeDataString(kvp[0])) : string.Empty;
            builder.Append(key);
            if (kvp.Length == 2)
            {
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(Uri.UnescapeDataString(kvp[1])));
            }
        }

        return builder.ToString();
    }

    private IEnumerator DownloadImageAttempt(string resolvedUrl, string originalUrl, Image image, System.Action a, bool calc, RectTransform t, HexCell cell, List<Transform> walls, System.Action<bool> completion)
    {
        if (image == null)
        {
            completion(false);
            yield break;
        }

        string ext = string.Empty;
        Debug.Log($"[HexGrid] DownloadImage attempt url={resolvedUrl} original='{originalUrl}' calc={calc}");
        try
        {
            var uri = new Uri(resolvedUrl);
            ext = Path.GetExtension(uri.AbsolutePath);
        }
        catch (UriFormatException)
        {
            var cleanUrl = resolvedUrl.Split('?')[0];
            ext = Path.GetExtension(cleanUrl);
        }

        if (string.IsNullOrEmpty(ext))
        {
            ext = ".png";
        }

        string path = Path.Combine(Application.persistentDataPath, BuildCacheFileName(resolvedUrl, ext));
        Debug.Log($"[HexGrid] DownloadImage cache path={path}");

        if (TryLoadCachedTexture(path, image, a, calc, t, cell, walls, resolvedUrl, originalUrl))
        {
            Debug.Log($"[HexGrid] DownloadImage cache hit url={resolvedUrl}");
            completion(true);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(resolvedUrl))
        {
            Debug.Log($"[HexGrid] DownloadImage request sending url={resolvedUrl}");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[HexGrid] DownloadImage failed url={resolvedUrl} (original='{originalUrl}') error={request.error}");
                completion(false);
                yield break;
            }

            var data = request.downloadHandler.data;
            if (data != null && data.Length > 0)
            {
                try
                {
                    File.WriteAllBytes(path, data);
                    Debug.Log($"[HexGrid] DownloadImage cached url={resolvedUrl} bytes={data.Length}");
                }
                catch (IOException ioEx)
                {
                    Debug.LogError($"Failed to cache image '{resolvedUrl}' to '{path}': {ioEx.Message}");
                }
            }

            var tex = DownloadHandlerTexture.GetContent(request);
            if (tex == null)
            {
                Debug.LogWarning($"[HexGrid] DownloadImage received null texture url={resolvedUrl}");
                completion(false);
                yield break;
            }

            Debug.Log($"[HexGrid] DownloadImage download complete url={resolvedUrl} size={tex.width}x{tex.height}");
            ApplyTexture(tex, image, a, calc, t, cell, walls, resolvedUrl, originalUrl);
            completion(true);
        }
    }

    private string BuildCacheFileName(string url, string ext)
    {
        if (string.IsNullOrEmpty(url)) url = string.Empty;
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString() + ext;
        }
    }

    private bool TryLoadCachedTexture(string path, Image image, System.Action a, bool calc, RectTransform t, HexCell cell, List<Transform> walls, string resolvedUrl, string originalUrl)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var data = File.ReadAllBytes(path);
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning($"[HexGrid] Cached texture empty deleting path={path}");
                File.Delete(path);
                return false;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, data))
            {
                Debug.LogWarning($"[HexGrid] Cached texture invalid deleting path={path}");
                UnityEngine.Object.Destroy(tex);
                File.Delete(path);
                return false;
            }

            Debug.Log($"[HexGrid] Cached texture loaded url={resolvedUrl} (original='{originalUrl}') size={tex.width}x{tex.height}");
            ApplyTexture(tex, image, a, calc, t, cell, walls, resolvedUrl, originalUrl);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load cached image '{path}': {ex.Message}");
            try { File.Delete(path); }
            catch { }
        }
        return false;
    }

    private void ApplyTexture(Texture2D tex, Image image, System.Action a, bool calc, RectTransform t, HexCell cell, List<Transform> walls, string resolvedUrl, string originalUrl)
    {
        if (tex == null || image == null) return;

        Debug.Log($"[HexGrid] ApplyTexture url={resolvedUrl} (original='{originalUrl}') image={(image != null ? image.name : "null")} size={tex.width}x{tex.height} calc={calc}");
        image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        if (a != null) a();

        if (t != null) CalcSize((float)tex.width, (float)tex.height, t, cell, calc, walls, resolvedUrl);
    }

    private string ResolveNftUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        if (!url.Contains("%"))
        {
            return url;
        }

        try
        {
            var decoded = System.Uri.UnescapeDataString(url);
            return string.IsNullOrEmpty(decoded) ? url : decoded;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HexGrid] ResolveNftUrl failed url='{url}' error={ex.Message}");
            return url;
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
        Debug.Log($"[HexGrid] CalcSize url={url} origSize={width}x{height} normalized={w}x{h} calc={calc} cell={cell.coordinates.ToStringOnSeparateLines()}");
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
                string encodedUrl = UnityWebRequest.EscapeURL(url);
                Debug.Log($"[HexGrid] CalcSize auto-set url={url} wall={cell.index} pos=({w},{h}) scale=1");
                await Helper.Post("http://45.132.107.107/index/SetPosition", $"address={cell.coordinates.address}&image={encodedUrl}&x={w.ToString().Replace(",", ".")}&y={h.ToString().Replace(",", ".")}&scale={1}&index={cell.index}");
            }
            else
            {
                Debug.LogWarning($"[HexGrid] CalcSize wall index overflow url={url} index={cell.index} walls={walls.Count}");
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
            var rawUrl = item["url"].ToString();
            var normalizedUrl = ResolveNftUrl(rawUrl);
            if (normalizedUrl != rawUrl)
            {
                Debug.Log($"[HexGrid] GetNft normalized url from '{rawUrl}' to '{normalizedUrl}'");
            }
            si.url = normalizedUrl;
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
        var normalizedUrl = ResolveNftUrl(url);
        if (normalizedUrl != url)
        {
            Debug.Log($"[HexGrid] AddNft normalized url from '{url}' to '{normalizedUrl}'");
        }
        Debug.Log($"[HexGrid] AddNft url={normalizedUrl} cell={cell.coordinates.ToStringOnSeparateLines()} floor={floor} wallIndex={index} initial=({ix},{iy}) scale={iscale}");
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
            di.url = normalizedUrl;
            di.index = index;
            di.walls = walls;
            Debug.Log($"[HexGrid] AddNft DragImage enabled url={normalizedUrl}");
        }

        var image = img_o.AddComponent<Image>();
        Debug.Log($"[HexGrid] AddNft start download url={normalizedUrl} calc={calc}");
        StartCoroutine(DownloadImage(normalizedUrl, image, null, calc, t, cell, walls));
    }

    //TODO: îïòèìèçèðîâàòü
    public async void GetRooms()
    {
        var j = await Helper.Post("http://45.132.107.107/index/GetRooms", $"");
        Debug.Log($"[HexGrid] GetRooms start total={j["rooms"].Count()}");
        foreach (var item in j["rooms"])
        {
            int x = (int)item["x"];
            int y = (int)item["y"];
            int z = (int)item["z"];
            string address = item["address"].ToString();
            var cell = cells.FirstOrDefault(q => q.coordinates.x == x && q.coordinates.y == y && q.coordinates.z == z);
            Debug.Log($"[HexGrid] GetRooms room x={x} y={y} z={z} address={address}");
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
                    if (!string.IsNullOrEmpty(url))
                    {
                        Debug.Log($"[HexGrid] GetRooms add image room={id} url={url} coords=({ix},{iy}) scale={iscale} wall={index} floor={floor}");
                        AddNft(url, cell, ix, iy, iscale, index, floor);
                    }
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
