using Newtonsoft.Json;
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
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) Debug.Log(request.error);
        else
        {
            var tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
            image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            //if (t != null)
            {
                //   
                float w = tex.width / 1000f;
                float h = tex.height / 1000f;
                if (w < 1) { float delta = (float)tex.width / (float)tex.height; w = 1; h = w / delta; }
                if (h < 1) { float delta = (float)tex.height / (float)tex.width; h = 1; w = h / delta; }
                t.sizeDelta = new Vector2(w, h);
            }
        }
    }

    IEnumerator DownloadImage(string url, ProceduralImage image)
    {
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) Debug.Log(request.error);
        else
        {
            var tex = ((DownloadHandlerTexture)request.downloadHandler).texture;
            image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }

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
