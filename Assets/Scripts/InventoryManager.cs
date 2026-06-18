using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InventoryManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://onlineprogramming-fab1c-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text HealthPotionCountText;  
    [SerializeField] Text IronSwordCountText;    
    [SerializeField] Text DragonArmorCountText;   
    [SerializeField] Text MesTxt;

    string userKey;
    Dictionary<string, int> inventory = new Dictionary<string, int>();
    bool isUsing = false;//중복 방지

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MesTxt.text = "로그인 정보가 없습니다.";
            return;
        }

        LoadInventory();
    }

    void LoadInventory()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "가방을 열지 못했습니다.";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                //null방지용
                if (snapshot.Value == null)
                {
                    inventory = new Dictionary<string, int>();
                    inventory["HealthPotion"] = 0;
                    inventory["IronSword"] = 0;
                    inventory["DragonArmor"] = 0;

                    dispatcher.Enqueue(() =>
                    {
                        RefreshUI();
                        MesTxt.text = "가방이 비어있습니다.";
                    });
                    return;
                }

                string inventoryJson = snapshot.Value.ToString();

                //null방지 빈 문자열
                if (!string.IsNullOrEmpty(inventoryJson))
                    inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);
                else
                    inventory = new Dictionary<string, int>();

                //역직렬화 실패시 방지용
                if (inventory == null)
                {
                    inventory = new Dictionary<string, int>();
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MesTxt.text = "가방 열기 성공!";
                });
            });
    }

    void RefreshUI()
    {
        HealthPotionCountText.text = "HealthPotion : " + GetItemCount("HealthPotion");
        IronSwordCountText.text = "IronSword : " + GetItemCount("IronSword");
        DragonArmorCountText.text = "DragonArmor : " + GetItemCount("DragonArmor");
    }

    int GetItemCount(string itemName)
    {
        if (inventory.ContainsKey(itemName))
            return inventory[itemName];
        return 0;
    }

    public void OnClickUseHealthPotion()
    {
        UseItem("HealthPotion", "체력 회복 완료! HP +50");
    }

    public void OnClickUseIronSword()
    {
        UseItem("IronSword", "철검을 장착했습니다! ATK +30");
    }

    public void OnClickUseDragonArmor()
    {
        UseItem("DragonArmor", "드래곤 갑옷을 장착했습니다! DEF +70");
    }

    void UseItem(string itemName, string useMessage)
    {
        if (isUsing)
            return;

        isUsing = true;

        if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
        {
            MesTxt.text = itemName + " 이(가) 없습니다.";
            return;
        }

        inventory[itemName]--;
        SaveInventory(itemName, useMessage);
    }

    void SaveInventory(string usedItemName, string useMessage)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .SetValueAsync(inventoryJson)
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MesTxt.text = "가방 저장 실패";
                        isUsing = false;
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MesTxt.text = useMessage;
                    isUsing = false;
                });
            });
    }

    public void OnClickGoToShop()
    {
        SceneManager.LoadScene("ShopScene");
    }
}