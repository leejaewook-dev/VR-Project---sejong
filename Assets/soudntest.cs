using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem; // 입력 시스템
using UnityEngine.Networking;

public class SimpleMic : MonoBehaviour
{
    [Header("설정")]
    public string serverUrl = "http://192.168.0.XX:5000/stt"; // 서버 주소
    public bool sendToServer = true; // 체크하면 녹음 끝날 때 서버로 전송, 끄면 녹음만 함 (테스트용)

    [Header("연결할 것들")]
    public TextMeshProUGUI screenText; // 화면 텍스트
    public AudioSource mySpeaker;      // 내 목소리 들려줄 스피커

    [Header("버튼 설정")]
    public InputActionProperty recordButton; // 녹음 버튼 (예: 트리거)
    public InputActionProperty playButton;   // 재생 버튼 (예: B버튼 or 그립)

    private AudioClip myClip;
    private string micName;
    private bool isRecording = false;

    void Start()
    {
        // 마이크 장치 찾기
        if (Microphone.devices.Length > 0)
            micName = Microphone.devices[0];
    }

    void Update()
    {
        // 1. 녹음 버튼 누름 -> 녹음 시작
        if (recordButton.action.WasPressedThisFrame() && !isRecording)
        {
            if (screenText) screenText.text = "녹음 중... (말하세요)";
            // 최대 10초, 44100Hz로 녹음 시작
            myClip = Microphone.Start(micName, false, 10, 44100);
            isRecording = true;
        }

        // 2. 녹음 버튼 뗌 -> 녹음 종료 (+ 서버 전송)
        if (recordButton.action.WasReleasedThisFrame() && isRecording)
        {
            Microphone.End(micName);
            isRecording = false;

            if (sendToServer)
            {
                if (screenText) screenText.text = "서버로 전송 중...";
                StartCoroutine(SendAudio());
            }
            else
            {
                if (screenText) screenText.text = "녹음 완료! (재생 버튼으로 들어보세요)";
            }
        }

        // 3. 재생 버튼 누름 -> 방금 녹음한거 들어보기
        if (playButton.action.WasPressedThisFrame())
        {
            if (myClip != null)
            {
                if (screenText) screenText.text = "다시 듣는 중...";
                mySpeaker.PlayOneShot(myClip); // 녹음된 클립 재생
            }
            else
            {
                if (screenText) screenText.text = "녹음된 소리가 없습니다.";
            }
        }
    }

    IEnumerator SendAudio()
    {
        byte[] wavData = GetWavBytes(myClip);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "voice.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                if (screenText) screenText.text = "서버 응답: " + www.downloadHandler.text;
            }
            else
            {
                if (screenText) screenText.text = "에러: " + www.error;
            }
        }
    }

    // WAV 변환 함수 (그대로 유지)
    byte[] GetWavBytes(AudioClip clip)
    {
        using (var stream = new MemoryStream())
        {
            var writer = new BinaryWriter(stream);
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            writer.Write(Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.UTF8.GetBytes("WAVE"));
            writer.Write(Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((ushort)(clip.channels * 2));
            writer.Write((ushort)16);
            writer.Write(Encoding.UTF8.GetBytes("data"));
            writer.Write(samples.Length * 2);

            foreach (var sample in samples) writer.Write((short)(sample * short.MaxValue));
            return stream.ToArray();
        }
    }
}