using UnityEngine;
using UnityEngine.UI;

public class MarketItemUI : MonoBehaviour
{
    [SerializeField] Text ItemInfoText;
    [SerializeField] Button BuyButton;

    string marketKey;
    string sellerKey;
    string sellerName;
    string itemName;
    int price;

    MarketManager marketManager;

    public void Setup(string marketKey, string sellerKey, string sellerName,
                      string itemName, int price, MarketManager manager)
    {
        this.marketKey = marketKey;
        this.sellerKey = sellerKey;
        this.sellerName = sellerName;
        this.itemName = itemName;
        this.price = price;
        this.marketManager = manager;

        ItemInfoText.text = sellerName + " | " + itemName + " | " + price + " Coin";
        BuyButton.onClick.AddListener(() => marketManager.OnClickBuy(marketKey, sellerKey, itemName, price));
    }
}
