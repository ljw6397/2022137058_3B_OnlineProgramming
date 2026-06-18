using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://onlineprogramming-fab1c-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text BestScoreText;
    [SerializeField] Text CurrentScoreText;
    [SerializeField] Text ResultText;
    [SerializeField] Text CoinText;

    string userKey;
    int currentCoin;
    int currentScore;  
    int bestScore;     //Firebase에서 불러온 최고 점수

    string[] choices = { "가위", "바위", "보" };

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            ResultText.text = "로그인이 필요합니다.";
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
                        ResultText.text = "데이터 로드 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());
                bestScore = int.Parse(snapshot.Child("Score").Value.ToString());

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    ResultText.text = "가위, 바위, 보 중 선택하세요!";
                });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "Coin : " + currentCoin;
        CurrentScoreText.text = "현재 점수 : " + currentScore;
        BestScoreText.text = "최고 점수 : " + bestScore;
    }

    // ─── 버튼 연결 ───────────────────────────────
    public void OnClickScissors() { Play("가위"); }
    public void OnClickRock() { Play("바위"); }
    public void OnClickPaper() { Play("보"); }

    void Play(string playerChoice)
    {
        // 상대방 랜덤 선택
        string enemyChoice = choices[Random.Range(0, choices.Length)];

        string result = GetResult(playerChoice, enemyChoice);

        if (result == "승리")
        {
            currentScore++;
            //게임이 끝났을 때 보상 코인 지급 - 승리 시 +50 코인
            currentCoin += 50;
            ResultText.text = "나 : " + playerChoice + " / 상대 : " + enemyChoice + "\n🎉 승리! +50 골드";
            //점수를 Firebase에 저장 & 기존 점수보다 높을 때만 최고 점수 갱신
            SaveGameResult();
        }
        else if (result == "패배")
        {
            ResultText.text = "나 : " + playerChoice + " / 상대 : " + enemyChoice + "\n😢 패배...";
            currentScore = 0; // 지면 현재 점수 리셋
            RefreshUI();
        }
        else
        {
            ResultText.text = "나 : " + playerChoice + " / 상대 : " + enemyChoice + "\n🤝 무승부!";
            RefreshUI();
        }
    }

    string GetResult(string player, string enemy)
    {
        if (player == enemy) return "무승부";

        if ((player == "가위" && enemy == "보") ||
            (player == "바위" && enemy == "가위") ||
            (player == "보" && enemy == "바위"))
            return "승리";

        return "패배";
    }

    void SaveGameResult()
    {
        Dictionary<string, object> updateData = new Dictionary<string, object>();
        //게임이 끝났을 때 보상 코인 지급 - 코인 Firebase에 저장
        updateData["Coin"] = currentCoin;

        //현재 점수가 최고 점수보다 높을 때만 갱신
        if (currentScore > bestScore)
        {
            bestScore = currentScore;

            //점수를 Firebase에 저장
            updateData["Score"] = bestScore;
        }

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData) //점수와 코인 Firebase에 저장
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        ResultText.text = "저장 오류 발생";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                });
            });
    }

    public void OnClickGoToShop()
    {
        SceneManager.LoadScene("ShopScene");
    }
}
