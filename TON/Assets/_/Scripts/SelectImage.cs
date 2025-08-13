using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectImage : MonoBehaviour, IPointerClickHandler
{
    Init init;
    Transform nft;
    public string url;
    public bool selected;
    public string description;
    public string link;

    private void Start()
    {
        init = GameObject.Find("Init").GetComponent<Init>();
        nft = transform.Find("nft");
    }

    void Update()
    {
        
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var remove = init.PreviewPanel.transform.Find("nft(Clone)");
        if (remove != null) Destroy(remove.gameObject);

        var clone = Instantiate(nft);
        clone.SetParent(init.PreviewPanel.transform);
        var t = clone.GetComponent<RectTransform>();
        var image = clone.GetComponent<Image>();
        t.sizeDelta = Helper.ImageRatio(image, 200);
        t.anchorMin = new Vector2(0.5f, 0.5f);
        t.anchorMax = new Vector2(0.5f, 1);
        t.localPosition = Vector2.zero;
        t.anchoredPosition = Vector2.zero;
        t.SetTop(0);
        t.SetBottom(0);

        init.ButtonPlaceNft.SetActive(!selected);
        init.ButtonRemoveNft.SetActive(selected);
        init.PreviewDescription.text = description;
        init.PreviewLink.text = link;
        init.SelectedImage = this.transform;
        /*selected = !selected;
        if (selected) image.color = new Color32(0, 0, 0, 100);
        else image.color = new Color32(0, 0, 0, 30);*/
    }
}
