using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserRegister : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://onlineprogramming-fab1c-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] InputField NickNameIP;
    [SerializeField] Text CheckingTxt;

    [Header("Scene")]
    [SerializeField] string NextSceneName = "ShopScene";
    [SerializeField] bool LoadNextSceneAfterRegister = false;

    bool isProcessing = false; //무한 회원가입 중복 방지

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();
    }

    // 회원가입 버튼에 연결
    public void OnClickRegister()
    {
        if (isProcessing) return;
        isProcessing = true;

        string nickName = NickNameIP.text.Trim();

        if (string.IsNullOrEmpty(nickName))
        {
            CheckingTxt.text = "닉네임을 입력하라.";
            isProcessing = false;
            return;
        }

        CheckDuplicateNickName(nickName);
    }

    void CheckDuplicateNickName(string nickName)
    {
        reference
            .Child("UserInfo")
            .OrderByChild("NickName")
            .EqualTo(nickName)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    dispatcher.Enqueue(() =>
                    {
                        CheckingTxt.text = "서버가 연결이 오류남";
                        isProcessing = false;
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.Exists && snapshot.ChildrenCount > 0)
                {
                    dispatcher.Enqueue(() =>
                    {
                        CheckingTxt.text = "이미 있음.";
                        isProcessing = false;
                    });
                    return;
                }

                CreateUser(nickName);
            });
    }

    void CreateUser(string nickName)
    {
        DatabaseReference newUserRef = reference.Child("UserInfo").Push();
        string userKey = newUserRef.Key;

        UserData userData = new UserData(nickName);
        string json = JsonConvert.SerializeObject(userData);

        newUserRef.SetRawJsonValueAsync(json).ContinueWith(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                dispatcher.Enqueue(() =>
                {
                    CheckingTxt.text = "가입 중 오류남.";
                    isProcessing = false;
                });
                return;
            }

            dispatcher.Enqueue(() =>
            {
                PlayerPrefs.SetString("UserKey", userKey);
                PlayerPrefs.SetString("UserNickName", nickName);
                PlayerPrefs.Save();

                CheckingTxt.text = "가입완료 하이하이.";
                isProcessing = false;

                if (LoadNextSceneAfterRegister)
                    SceneManager.LoadScene(NextSceneName);
            });
        });
    }
}
