using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserLogin : MonoBehaviour
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
    [SerializeField] string NextSceneName = "MainScene";
    [SerializeField] bool LoadNextSceneAfterLogin = false;

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();
    }

    // 로그인 버튼에 연결
    public void OnClickLogin()
    {
        string nickName = NickNameIP.text.Trim();

        if (string.IsNullOrEmpty(nickName))
        {
            CheckingTxt.text = "닉네임 입력해라.";
            return;
        }

        Login(nickName);
    }

    void Login(string nickName)
    {
        reference
            .Child("UserInfo")
            .OrderByChild("NickName")
            .EqualTo(nickName)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled) //오류 방지
                {
                    dispatcher.Enqueue(() =>
                    {
                        CheckingTxt.text = "서버가 오류남";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (!snapshot.HasChildren)
                {
                    dispatcher.Enqueue(() =>
                    {
                        CheckingTxt.text = "등록안됐음.";
                    });
                    return;
                }

                foreach (DataSnapshot userSnapshot in snapshot.Children)
                {
                    string userKey = userSnapshot.Key;

                    dispatcher.Enqueue(() =>
                    {
                        PlayerPrefs.SetString("UserKey", userKey);
                        PlayerPrefs.SetString("UserNickName", nickName);
                        PlayerPrefs.Save();

                        CheckingTxt.text = "로그인성공!";

                        if (LoadNextSceneAfterLogin)
                        {
                            SceneManager.LoadScene(NextSceneName);
                        }
                    });

                    break;
                }
            });
    }
}
