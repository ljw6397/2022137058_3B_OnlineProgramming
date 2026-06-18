using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MarketManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://onlineprogramming-fab1c-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Dropdown MyItemDropdown;
    [SerializeField] InputField PriceInputField;
    [SerializeField] Transform MarketContent;
    [SerializeField] GameObject MarketItemPrefab;
    [SerializeField] Text MesTxt;
    [SerializeField] Text GoldText;

    string userKey;
    string userNickName;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    // 판매 가능한 아이템 목록 (인벤토리에서 1개 이상인 것)
    List<string> sellableItems = new List<string>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");
        userNickName = PlayerPrefs.GetString("UserNickName");

        if (string.IsNullOrEmpty(userKey))
        {
            MesTxt.text = "로그인이 필요합니다.";
            return;
        }

        LoadUserData();
    }

    void RefreshUI()
    {

        GoldText.text = "Coin : " + currentCoin;
    }

    // ─── 유저 데이터 불러오기 ───────────────────────────────
    void LoadUserData()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "데이터 로드 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                //snapshot 자체가 없을 때 방어
                if (!snapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "유저 데이터를 찾을 수 없습니다.";
                    });
                    return;
                }

                //Coin 노드 null 방어
                if (snapshot.Child("Coin").Exists && snapshot.Child("Coin").Value != null)
                    currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());
                else
                    currentCoin = 0;

                //Inventory 노드 null 방어
                if (snapshot.Child("Inventory").Exists && snapshot.Child("Inventory").Value != null)
                {
                    string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                    if (!string.IsNullOrEmpty(inventoryJson))
                        inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
                    else
                        inventory = new Dictionary<string, int>();
                }

                //역직렬화 실패 방어
                if (inventory == null)
                    inventory = new Dictionary<string, int>();

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    SetupDropdown();
                    LoadMarketItems();
                    MesTxt.text = "거래소에 오신 것을 환영합니다!";
                });
            });
    }

    // ─── 드롭다운 설정 (판매 가능한 아이템만) ───────────────────────────────
    void SetupDropdown()
    {
        sellableItems.Clear();
        MyItemDropdown.ClearOptions();

        List<string> options = new List<string>();

        foreach (var item in inventory)
        {
            if (item.Value > 0)
            {
                sellableItems.Add(item.Key);
                options.Add(item.Key + " (" + item.Value + "개)");
            }
        }

        if (options.Count == 0)
        {
            options.Add("판매할 아이템 없음");
        }

        MyItemDropdown.AddOptions(options);
    }

    // ─── 판매 등록 ───────────────────────────────
    public void OnClickRegister()
    {
        if (sellableItems.Count == 0)
        {
            MesTxt.text = "판매할 아이템이 없습니다.";
            return;
        }

        string selectedItem = sellableItems[MyItemDropdown.value];

        if (!int.TryParse(PriceInputField.text.Trim(), out int price) || price <= 0)
        {
            MesTxt.text = "올바른 가격을 입력해주세요.";
            return;
        }

        //내 아이템을 판매 등록 - 인벤토리에서 아이템 차감
        inventory[selectedItem]--;

        // Firebase에 판매 등록
        RegisterItem(selectedItem, price);
    }

    void RegisterItem(string itemName, int price)
    {
        // 인벤토리 저장
        string inventoryJson = JsonConvert.SerializeObject(inventory);
        Dictionary<string, object> userUpdate = new Dictionary<string, object>();
        userUpdate["Inventory"] = inventoryJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(userUpdate)
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "판매 등록 실패";
                        inventory[itemName]++; 
                    });
                    return;
                }

                //내 아이템을 판매 등록 - Market 노드에 판매 정보 저장
                DatabaseReference newMarketRef = reference.Child("Market").Push();

                Dictionary<string, object> marketData = new Dictionary<string, object>();
                marketData["sellerKey"] = userKey;
                marketData["sellerName"] = userNickName;
                marketData["itemName"] = itemName;
                marketData["price"] = price;
                marketData["isSold"] = false; //판매 완료 처리 - 초기값 false

                newMarketRef.SetValueAsync(marketData).ContinueWith(marketTask =>
                {
                    if (marketTask.IsFaulted || marketTask.IsCanceled)
                    {
                        inventory[itemName]++; //차감했던 아이템 되돌리기
                        string rollbackJson = JsonConvert.SerializeObject(inventory);
                        reference
                            .Child("UserInfo")
                            .Child(userKey)
                            .Child("Inventory")
                            .SetValueAsync(rollbackJson); //Firebase에도 롤백
                        dispatcher.Enqueue(() =>
                        {
                            RefreshUI();
                            SetupDropdown();
                            MesTxt.text = "거래소 등록 실패";
                        });
                        return;
                    }

                    dispatcher.Enqueue(() =>
                    {
                        RefreshUI(); 
                        SetupDropdown();
                        LoadMarketItems();
                        MesTxt.text = itemName + " 판매 등록 완료!";
                    });
                });
            });
    }

    // ─── 거래소 아이템 목록 불러오기 ───────────────────────────────
    void LoadMarketItems()
    {
        foreach (Transform child in MarketContent)
            Destroy(child.gameObject);

        reference
            .Child("Market")
            .OrderByChild("isSold")
            .EqualTo(false)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "거래소 목록 로드 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.HasChildren)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "등록된 아이템이 없습니다.";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    foreach (DataSnapshot item in snapshot.Children)
                    {
                        string marketKey = item.Key;
                        string sellerKey = item.Child("sellerKey").Value.ToString();
                        string sellerName = item.Child("sellerName").Value.ToString();
                        string itemName = item.Child("itemName").Value.ToString();
                        int price = int.Parse(item.Child("price").Value.ToString());

                        // 내가 올린 아이템은 구매 버튼 비활성화
                        GameObject obj = Instantiate(MarketItemPrefab, MarketContent);
                        MarketItemUI ui = obj.GetComponent<MarketItemUI>();
                        ui.Setup(marketKey, sellerKey, sellerName, itemName, price, this);

                        // 내 아이템이면 구매 버튼 숨기기
                        if (sellerKey == userKey)
                            obj.GetComponentInChildren<Button>().interactable = false;
                    }
                });
            });
    }

    // ─── 아이템 구매 ───────────────────────────────
    public void OnClickBuy(string marketKey, string sellerKey, string itemName, int price)
    {
        // 내 아이템 구매 방지
        if (sellerKey == userKey)
        {
            MesTxt.text = "내 아이템은 구매할 수 없습니다.";
            return;
        }

        //구매자 코인 감소 - 코인 체크
        if (currentCoin < price)
        {
            MesTxt.text = "골드가 부족합니다.";
            return;
        }

        //구매자 코인 감소
        currentCoin -= price;

        //구매자 인벤토리 증가
        if (inventory.ContainsKey(itemName))
            inventory[itemName]++;
        else
            inventory[itemName] = 1;

        ProcessPurchase(marketKey, sellerKey, itemName, price);
    }

    void ProcessPurchase(string marketKey, string sellerKey, string itemName, int price)
    {
        //판매 완료 처리 - isSold = true 로 변경
        reference
            .Child("Market")
            .Child(marketKey)
            .Child("isSold")
            .SetValueAsync(true)
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "구매 처리 실패";
                        // 롤백
                        currentCoin += price;  
                        inventory[itemName]--;
                    });
                    return;
                }

                // 2. 구매자 코인 & 인벤토리 저장
                string inventoryJson = JsonConvert.SerializeObject(inventory);
                Dictionary<string, object> buyerUpdate = new Dictionary<string, object>();
                buyerUpdate["Coin"] = currentCoin;  //구매자 코인 감소 저장
                buyerUpdate["Inventory"] = inventoryJson; //구매자 인벤토리 증가 저장

                reference
                    .Child("UserInfo")
                    .Child(userKey)
                    .UpdateChildrenAsync(buyerUpdate)
                    .ContinueWith(buyerTask =>
                    {
                        if (buyerTask.IsFaulted || buyerTask.IsCanceled)
                        {
                            reference
                                .Child("Market")
                                .Child(marketKey)
                                .Child("isSold")
                                .SetValueAsync(false); 

                            dispatcher.Enqueue(() =>
                            {
                                MesTxt.text = "구매자 데이터 저장 실패";
                            });
                            return;
                        }

                        //판매자 코인 증가 - 판매자 현재 코인 불러오기
                        reference
                            .Child("UserInfo")
                            .Child(sellerKey)
                            .Child("Coin")
                            .GetValueAsync()
                            .ContinueWith(sellerTask =>
                            {
                                if (sellerTask.IsFaulted || sellerTask.IsCanceled)
                                {
                                    dispatcher.Enqueue(() =>
                                    {
                                        MesTxt.text = "판매자 데이터 로드 실패";
                                    });
                                    return;
                                }
                                //판매자 코인 증가 - 코인 더하고 저장
                                int sellerCoin = int.Parse(sellerTask.Result.Value.ToString());
                                sellerCoin += price;

                                reference
                               .Child("UserInfo")
                               .Child(sellerKey)
                               .Child("Coin")
                               .SetValueAsync(sellerCoin)
                               .ContinueWith(finalTask =>
                               {
                                 if (finalTask.IsFaulted || finalTask.IsCanceled)
                               {
                                 dispatcher.Enqueue(() =>
                               {
                                  MesTxt.text = "판매자 코인 저장 실패";
                               });
                                   return;
                                   }

                                 dispatcher.Enqueue(() =>
                                 {
                                  //내가 판매자면 currentCoin도 갱신
                                  if (sellerKey == userKey)
                                 {
                                   currentCoin = sellerCoin;
                                  }

                                  RefreshUI();
                                  LoadMarketItems();
                                  MesTxt.text = itemName + " 구매 완료!";
                                });
                               });
                            });
                    });
            });
    }

    public void OnClickGoToShop()
    {
        SceneManager.LoadScene("ShopScene");
    }

    public void OnClickRefresh()
    {
        LoadUserData(); //코인도 새로고침
        LoadMarketItems();
        MesTxt.text = "목록 새로고침 완료!";
    }
}