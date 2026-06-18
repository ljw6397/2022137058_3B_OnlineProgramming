using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ShopManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://onlineprogramming-fab1c-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text GoldText;
    [SerializeField] Text MesTxt;

    string userKey;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();
    Dictionary<string, bool> unitList = new Dictionary<string, bool>();
    bool isBuying = false;

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MesTxt.text = "로그인이 필요합니다.";
            return;
        }

        LoadUserData();
    }

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

                if (!snapshot.Exists)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "유저 데이터를 찾을 수 없습니다.";
                    });
                    return;
                }

                //null 방어코드
                if (snapshot.Child("Coin").Exists && snapshot.Child("Coin").Value != null)
                    currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());
                else
                    currentCoin = 0;

                string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                if (!string.IsNullOrEmpty(inventoryJson))
                    inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
                else
                    inventory = new Dictionary<string, int>();

                if (inventory == null)
                    inventory = new Dictionary<string, int>();

                //UnitList 불러오기
                string unitListJson = snapshot.Child("UnitList").Value.ToString();
                if (!string.IsNullOrEmpty(unitListJson))
                    unitList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(unitListJson);
                else
                    unitList = new Dictionary<string, bool>();

                if (unitList == null)
                    unitList = new Dictionary<string, bool>();

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MesTxt.text = "데이터 로드 성공!";
                });
            });
    }

    void RefreshUI()
    {
        GoldText.text = "Coin : " + currentCoin;
    }

    // ─── 아이템 구매 ───────────────────────────────
    public void OnClickBuyHealthPotion()
    {
        BuyItem("HealthPotion", 100);
    }

    public void OnClickBuyIronSword()
    {
        BuyItem("IronSword", 250);
    }

    public void OnClickBuyDragonArmor()
    {
        BuyItem("DragonArmor", 300);
    }

    void BuyItem(string itemName, int price)
    {
        if (isBuying)
            return;

        isBuying = true;

        if (currentCoin < price)
        {
            MesTxt.text = "돈이 부족합니다.";
            return;
        }

        currentCoin -= price;

        if (inventory.ContainsKey(itemName))
            inventory[itemName]++;
        else
            inventory[itemName] = 1;

        SaveItemData(itemName);
    }

    void SaveItemData(string boughtItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["Inventory"] = inventoryJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "저장 오류 발생";
                        isBuying = false;//중복방지
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MesTxt.text = boughtItemName + " 구입 성공!";
                    isBuying = false; //중복방지
                });
            });
    }

    //Unit2, Unit3, Unit4 등을 코인으로 구매
    public void OnClickBuyUnit2() { BuyUnit("Unit2", 50); }
    public void OnClickBuyUnit3() { BuyUnit("Unit3", 100); }
    public void OnClickBuyUnit4() { BuyUnit("Unit4", 150); }
    public void OnClickBuyUnit5() { BuyUnit("Unit5", 180); }

    void BuyUnit(string unitName, int price)
    {
        //코인으로 구매
        if (currentCoin < price)
        {
            MesTxt.text = "돈이 부족합니다.";
            return;
        }

        //이미 보유한 유닛은 다시 구매할 수 없게 처리
        if (unitList.ContainsKey(unitName) && unitList[unitName] == true)
        {
            MesTxt.text = unitName + " 은(는) 이미 보유 중입니다.";
            return;
        }
        //코인 차감
        currentCoin -= price;
        //UnitList JSON 값을 수정 - 유닛 보유 상태 true로 변경
        unitList[unitName] = true;

        SaveUnitData(unitName);
    }

    void SaveUnitData(string boughtUnitName)
    {
        //UnitList JSON 값을 수정하여 Firebase에 저장 - Dictionary를 JSON 문자열로 변환
        string unitListJson = JsonConvert.SerializeObject(unitList);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["UnitList"] = unitListJson; //UnitList JSON 값을 수정하여 Firebase에 저장

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData) //Firebase에 저장
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "저장 오류 발생";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MesTxt.text = boughtUnitName + " 유닛 구매 완료!";
                });
            });
    }

    public void OnClickGoToInventory()
    {
        SceneManager.LoadScene("InventoryScene");
    }
    public void OnClickGoToGameScene()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void OnClickGoToMarketScene()
    {
        SceneManager.LoadScene("MarketScene");
    }
}