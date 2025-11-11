using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.UI.ProceduralImage;

public class Init : MonoBehaviour
{
    public Texture2D cursorHand;
    public GameObject mobileCanvas, jump, ok;
    public DragImage image;

#if UNITY_WEBGL && !UNITY_EDITOR
    private class WebImageRequest
    {
        public Action<Texture2D> OnSuccess;
        public Action<string> OnError;
    }

    private const float WebImageTimeoutSeconds = 15f;
    private static readonly Dictionary<string, WebImageRequest> webImageRequests = new Dictionary<string, WebImageRequest>();
#endif

    void Awake()
    {
        Helper.init = this;
        /*var buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (var button in buttons)
        {
            if (button.name == "ButtonAuth") ButtonAuth = button.gameObject;
        }*/
    }

    private void Start()
    {
        //ShowCursor();
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            if (Cursor.visible) { Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked; Helper.moveLock = false; }
            else { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; Helper.moveLock = true; }
        }
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Q)) GetNft("{\"items\":[{\"id\":32,\"address\":\"EQDaVOscxs5EoL2X84KQMl0dKL0NhPhsZGd00dMTqWGl834b\",\"x\":1.375,\"y\":-8.25,\"scale\":1,\"wall\":0,\"selected\":true,\"url\":\"http://localhost:20057/files/httpssgetgemsionfts62c262c2ac15ec4abf845acf14dbnft62c2abbfec4abf845acf14d4jpg.png\",\"name\":\"Officer Crya - promo\",\"description\":\"Example\",\"externalUrl\":\"https://getgems.io/nft/EQBUaoWjrE-sf6r619Zz6_M_Ue7hin0ju0d2tyErO2nJwW8h\"},{\"id\":34,\"address\":\"EQDaVOscxs5EoL2X84KQMl0dKL0NhPhsZGd00dMTqWGl834b\",\"x\":7.3125,\"y\":-7.25,\"scale\":1.8,\"wall\":0,\"selected\":true,\"url\":\"http://localhost:20057/files/httpscloudflare-ipfscomipfsQmeWgJuXujsfqcQCFxweLJzsHLMiHM2KYyGQAKTzS3uvUufilename=5_uQjb2a5png.png\",\"name\":\"Mr. O'Klock\",\"description\":\"One of the five main characters of the Cyber Dycks collection\",\"externalUrl\":null}],\"owner\":true}");
    }

    public void GetNft(string value)
    {
        var set = new Set();
        if (string.IsNullOrEmpty(value)) set.Items.Add(new ImageWeb { Url = "https://rooms.worldofton.ru/files/httpsnfttondiamondsnft29982998svg.png" });
        else set = JsonConvert.DeserializeObject<Set>(value);
        var walls = new List<Transform>();
        var walls_o = GameObject.Find("Walls");
        foreach (Transform tr in walls_o.transform)
            if (tr.tag == "wall")
            {
                foreach (Transform tr1 in tr) if (tr1.name == "nft") GameObject.Destroy(tr1.gameObject);
                walls.Add(tr);
            }
        foreach (var item in set.Items)
        {
            var img_o = new GameObject("nft");
            var t = img_o.AddComponent<RectTransform>();
            t.transform.SetParent(walls[item.Wall]);
            t.localEulerAngles = Vector3.zero;
            t.sizeDelta = new Vector2(0.1f, 0.1f);
            t.anchorMin = new Vector2(0, 1);
            t.anchorMax = new Vector2(0, 1);
            t.pivot = Vector2.zero;
            if (item.Scale == 0) t.localScale = Vector3.one;
            else t.localScale = new Vector3(item.Scale, item.Scale, 0);
            if (item.Wall == 0 && item.X == 0 && item.Y == 0) { item.X = 3; item.Y = -7.5f; }
            t.localPosition = new Vector3(item.X, item.Y, 0);
            t.anchoredPosition = new Vector2(item.X, item.Y);
            if (set.Owner)
            {
                var di = img_o.AddComponent<DragImage>();
                di.index = item.Wall;
                di.walls = walls;
                di.id = item.Id;
            }

            var image = img_o.AddComponent<Image>();
            StartCoroutine(DownloadImage(item.Url, image, t));
        }
    }

    IEnumerator DownloadImage(string url, Image image, RectTransform t)
    {
        Texture2D texture = null;
        string error = null;
        yield return LoadTextureFromUrl(url, tex => texture = tex, err => error = err);
        if (texture == null)
        {
            if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
            yield break;
        }

        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

        //Øèðèíà è âûñîòà êàðòèíêè
        float w = texture.width / 1000f;
        float h = texture.height / 1000f;
        if (w < 1)
        {
            float delta = (float)texture.width / (float)texture.height;
            w = 1;
            h = w / delta;
        }
        if (h < 1)
        {
            float delta = (float)texture.height / (float)texture.width;
            h = 1;
            w = h / delta;
        }
        t.sizeDelta = new Vector2(w, h);
    }

    IEnumerator DownloadImage(string url, ProceduralImage image)
    {
        Texture2D texture = null;
        string error = null;
        yield return LoadTextureFromUrl(url, tex => texture = tex, err => error = err);
        if (texture == null)
        {
            if (!string.IsNullOrEmpty(error)) Debug.LogError(error);
            yield break;
        }

        image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    IEnumerator LoadTextureFromUrl(string url, Action<Texture2D> onSuccess, Action<string> onError)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (ShouldUseWebpLoader(url))
        {
            Texture2D webpTexture = null;
            string webpError = null;
            yield return LoadTextureWebGl(url, tex => webpTexture = tex, err => webpError = err);
            if (webpTexture != null)
            {
                onSuccess?.Invoke(webpTexture);
                yield break;
            }

            if (!string.IsNullOrEmpty(webpError))
            {
                Debug.LogWarning($"WebP loader fallback for {url}: {webpError}");
            }
        }
#endif

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Failed to download image {url}: {request.error}");
            }
            else
            {
                onSuccess?.Invoke(DownloadHandlerTexture.GetContent(request));
            }
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private IEnumerator LoadTextureWebGl(string url, Action<Texture2D> onSuccess, Action<string> onError)
    {
        bool completed = false;
        string requestId = Helper.RandomString();
        webImageRequests[requestId] = new WebImageRequest
        {
            OnSuccess = texture =>
            {
                completed = true;
                onSuccess?.Invoke(texture);
            },
            OnError = message =>
            {
                completed = true;
                onError?.Invoke(message);
            }
        };

        PluginJS.LoadImageToPng(url, gameObject.name, nameof(OnWebImageLoaded), nameof(OnWebImageFailed), requestId);

        float startTime = Time.realtimeSinceStartup;
        yield return new WaitUntil(() => completed || Time.realtimeSinceStartup - startTime >= WebImageTimeoutSeconds);
        if (!completed)
        {
            webImageRequests.Remove(requestId);
            onError?.Invoke("Timeout while loading image");
        }
    }

    private bool ShouldUseWebpLoader(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;

        string checkUrl = url;
        int hashIndex = checkUrl.IndexOf('#');
        if (hashIndex >= 0) checkUrl = checkUrl.Substring(0, hashIndex);
        int queryIndex = checkUrl.IndexOf('?');
        if (queryIndex >= 0) checkUrl = checkUrl.Substring(0, queryIndex);

        if (checkUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return true;
        return url.IndexOf("webp", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void OnWebImageLoaded(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;

        int separatorIndex = payload.IndexOf('|');
        if (separatorIndex < 0) return;

        string requestId = payload.Substring(0, separatorIndex);
        string data = payload.Substring(separatorIndex + 1);

        if (!webImageRequests.TryGetValue(requestId, out var request)) return;
        webImageRequests.Remove(requestId);

        try
        {
            byte[] bytes = Convert.FromBase64String(data);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                request.OnError?.Invoke("Unable to decode image data");
                return;
            }

            request.OnSuccess?.Invoke(texture);
        }
        catch (Exception ex)
        {
            request.OnError?.Invoke(ex.Message);
        }
    }

    public void OnWebImageFailed(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return;

        int separatorIndex = payload.IndexOf('|');
        string requestId = separatorIndex >= 0 ? payload.Substring(0, separatorIndex) : payload;
        string message = separatorIndex >= 0 && payload.Length > separatorIndex + 1 ? payload.Substring(separatorIndex + 1) : "Unknown error";

        if (!webImageRequests.TryGetValue(requestId, out var request)) return;
        webImageRequests.Remove(requestId);
        request.OnError?.Invoke(message);
    }
#endif

    public void ShowCursor()
    {
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        WebGLInput.captureAllKeyboardInput = false;
        Helper.moveLock = true;
    }

    public void HideCursor()
    {
        Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked;
        WebGLInput.captureAllKeyboardInput = true;
        Helper.moveLock = false;
    }

    public void UserClick()
    {
        PluginJS.SendMessageToPage("ok");
    }

    public void UserMouseEnter()
    {
        Cursor.SetCursor(cursorHand, Vector2.zero, CursorMode.ForceSoftware);
    }

    public void UserMouseExit()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
    }

    public void SetData(string s)
    {
        var split = s.Split('|');
        string avatar = split[0];
        string name = split[1];
        GameObject.Find("UserText").GetComponent<TextMeshProUGUI>().text = name;
        var image = GameObject.Find("UserImage").GetComponent<ProceduralImage>();
        StartCoroutine(DownloadImage(avatar, image));
    }

    public void Mobile(string mobile)
    {
        Helper.mobile = mobile == "true";
        if (Helper.mobile) mobileCanvas.SetActive(true);
    }
}

public class ImageWeb
{
    public int Id { get; set; }
    public string Address { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Scale { get; set; }
    public int Wall { get; set; }
    public bool Selected { get; set; }
    public string Url { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string ExternalUrl { get; set; }
}

public class Set
{
    public bool Owner { get; set; }
    public List<ImageWeb> Items { get; set; }

    public Set()
    {
        Items = new List<ImageWeb>();
    }
}
