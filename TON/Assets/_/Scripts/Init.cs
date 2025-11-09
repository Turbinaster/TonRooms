using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class Init : MonoBehaviour
{
	public GameObject NFTCollection;
    public GameObject NFTCollectionCanvas;
    public GameObject MainCanvas;
    public PlayerBehaviour Player;
	public GameObject MainCamera;
	public GameObject FreeCamera;
	public GameObject ButtonAuth;
	public GameObject ButtonNftCollection;
	public GameObject ButtonPlaceNft;
	public GameObject ButtonRemoveNft;
    public GameObject PreviewPanel;
	public TextMeshProUGUI PreviewDescription;
	public TextMeshProUGUI PreviewLink;

    public Transform SelectedImage;
    public HexGrid HexGrid;

    void Awake()
    {
        Player = Resources.FindObjectsOfTypeAll<PlayerBehaviour>()[0];
        HexGrid = GameObject.Find("HexGrid").GetComponent<HexGrid>();

        var cameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (var camera in cameras)
        {
            if (camera.name == "FreeCamera") FreeCamera = camera.gameObject;
            else if (camera.name == "MainCamera") MainCamera = camera.gameObject;
        }

        var buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (var button in buttons)
        {
            if (button.name == "ButtonAuth") ButtonAuth = button.gameObject;
            if (button.name == "ButtonNftCollection") ButtonNftCollection = button.gameObject;
            if (button.name == "ButtonPlaceNft") ButtonPlaceNft = button.gameObject;
            if (button.name == "ButtonRemoveNft") ButtonRemoveNft = button.gameObject;
        }

        var canvas = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var canva in canvas)
        {
            if (canva.name == "NFT Collection Canvas") NFTCollectionCanvas = canva.gameObject;
            if (canva.name == "Main Canvas") MainCanvas = canva.gameObject;
        }

        var layouts = Resources.FindObjectsOfTypeAll<VerticalLayoutGroup>();
        foreach (var layout in layouts)
        {
            if (layout.name == "NFT Collection") NFTCollection = layout.gameObject;
        }

        var panels = Resources.FindObjectsOfTypeAll<RectTransform>();
        foreach (var panel in panels)
        {
            if (panel.name == "PreviewPanel") PreviewPanel = panel.gameObject;
        }

        var texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        foreach (var text in texts)
        {
            if (text.name == "PreviewDescription") PreviewDescription = text.GetComponent<TextMeshProUGUI>();
            if (text.name == "PreviewLink") PreviewLink = text.GetComponent<TextMeshProUGUI>();
        }

        if (!string.IsNullOrEmpty(PlayerPrefs.GetString("address")))
        {
            ButtonNftCollection.SetActive(true);
        }
    }

    async void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && NFTCollectionCanvas.activeSelf) CloseNftCollection();
        if (Input.GetKeyDown(KeyCode.KeypadPlus)) { await Helper.Post("http://45.132.107.107/index/AddFloor", $"address={PlayerPrefs.GetString("address")}"); HexGrid.GetRooms(); }
        if (Input.GetKeyDown(KeyCode.KeypadMinus)) { await Helper.Post("http://45.132.107.107/index/RemoveFloor", $"address={PlayerPrefs.GetString("address")}"); HexGrid.GetRooms(); }
    }

    public void CameraToggle()
    {
        if (!FreeCamera.activeSelf)
        {
            Player.gameObject.SetActive(false);
            MainCamera.SetActive(false);
            FreeCamera.SetActive(true);
        }
        else
        {
            Player.gameObject.SetActive(true);
            MainCamera.SetActive(true);
            FreeCamera.SetActive(false);
        }
    }

    public void SelectAll()
    {

    }

    public void UnselectAll()
    {

    }

    public async void SaveSelection()
    {
        var images = NFTCollection.transform.GetComponentsInChildren<SelectImage>().Where(x => x.selected).Select(x => UnityWebRequest.EscapeURL(x.url)).ToList();
        CloseNftCollection();
        await Helper.Post("http://45.132.107.107/index/SaveSelection", $"address={PlayerPrefs.GetString("address")}&images={string.Join("|", images)}");
    }

    public void CloseNftCollection()
    {
        NFTCollectionCanvas.SetActive(false);
        MainCanvas.SetActive(true);
        Player.gameObject.SetActive(true);
        Player.GetComponent<ThirdPersonController>().LockCameraPosition = false;
        Cursor.lockState = CursorLockMode.Locked;

        var remove = PreviewPanel.transform.Find("nft(Clone)");
        if (remove != null) Destroy(remove.gameObject);
        ButtonPlaceNft.SetActive(false);
        ButtonRemoveNft.SetActive(false);
        PreviewDescription.text = "";
        PreviewLink.text = "";
        SelectedImage = null;
    }

    public void OpenNftCollection()
    {
        NFTCollectionCanvas.SetActive(true);
        MainCanvas.SetActive(false);
        Player.gameObject.SetActive(false);
        Player.GetComponent<ThirdPersonController>().LockCameraPosition = true;
    }

    public async void PlaceNft()
    {
        var si = SelectedImage.GetComponent<SelectImage>();
        string encodedUrl = UnityWebRequest.EscapeURL(si.url);
        await Helper.Post("http://45.132.107.107/index/SetSelection", $"address={PlayerPrefs.GetString("address")}&image={encodedUrl}&selected=true&floor={Player.Floor()}");
        ButtonRemoveNft.SetActive(true);
        ButtonPlaceNft.SetActive(false);
        SelectedImage.GetComponent<Image>().color = new Color32(0, 0, 0, 100);
        si.selected = true;
        HexGrid.GetRooms();
    }

    public async void RemoveNft()
    {
        var si = SelectedImage.GetComponent<SelectImage>();
        string encodedUrl = UnityWebRequest.EscapeURL(si.url);
        await Helper.Post("http://45.132.107.107/index/SetSelection", $"address={PlayerPrefs.GetString("address")}&image={encodedUrl}&selected=false");
        ButtonRemoveNft.SetActive(false);
        ButtonPlaceNft.SetActive(true);
        SelectedImage.GetComponent<Image>().color = new Color32(0, 0, 0, 30);
        si.selected = false;
        HexGrid.GetRooms();
    }

    public void PreviewLinkClick()
    {
        Application.OpenURL(PreviewLink.text);
    }
}

public class HexCoordinates
{
	public int x { get; private set; }
	public int z { get; private set; }
	public int y { get { return -x - z; } }
	public string address;

	public HexCoordinates(int x, int z)
	{
		this.x = x;
		this.z = z;
	}

	public static HexCoordinates FromOffsetCoordinates(int x, int z)
	{
		return new HexCoordinates(x - z / 2, z);
	}

	public string ToStringOnSeparateLines()
	{
		return $"{x};{y};{z}";
	}
}
