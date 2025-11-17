using System.Collections;
using System.IO;
using System.Text;
using TMPro; // TextMeshPro를 쓰기 위해 꼭 필요!
using UnityEngine;
using UnityEngine.InputSystem; // 입력 시스템
using UnityEngine.Networking;

public class VoiceHandler : MonoBehaviour
{
    [Header("1. 서버 설정")]
    public string serverUrl = "http://YOUR_SERVER_IP:5000/stt"; // 본인 서버 주소

    [Header("2. 연결할 오브젝트 (필수!)")]
    public TextMeshProUGUI statusText; // "전광판"으로 쓸 텍스트
    public GameObject recordingIndicator; // "표시등"으로 쓸 오브젝트 (빨간 공)

    [Header("3. 컨트롤러 버튼 설정")]
    public InputActionProperty recordButton; // 녹음할 버튼 (A버튼 등)

    private AudioClip myClip;
    private string micName;
    private bool isRecording = false;

    void Start()
    {
        // 1. 마이크 찾기
        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
        }
        else
        {
            if (statusText) statusText.text = "마이크를 찾을 수 없습니다!";
        }

        // 2. 시작할 때 "표시등"은 무조건 끄기
        if (recordingIndicator)
        {
            recordingIndicator.SetActive(false);
        }
    }

    void Update()
    {
        // 1. 녹음 버튼을 "누르는 순간"
        if (recordButton.action.WasPressedThisFrame() && !isRecording)
        {
            if (statusText) statusText.text = "Recording";
            if (recordingIndicator) recordingIndicator.SetActive(true); // ★ 피드백 켜기

            myClip = Microphone.Start(micName, false, 10, 44100); // 10초 녹음
            isRecording = true;
        }

        // 2. 녹음 버튼을 "떼는 순간"
        if (recordButton.action.WasReleasedThisFrame() && isRecording)
        {
            Microphone.End(micName);
            isRecording = false;

            if (statusText) statusText.text = "Loading";
            if (recordingIndicator) recordingIndicator.SetActive(false); // ★ 피드백 끄기

            // 서버로 전송 시작
            StartCoroutine(SendAudioToServer());
        }
    }

    IEnumerator SendAudioToServer()
    {
        // 1. 녹음된 소리를 .wav 파일 형태로 변환
        byte[] wavData = GetWavBytes(myClip);

        // 2. 서버로 보낼 폼 만들기
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "voice.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // ★ 성공! 서버가 보낸 텍스트를 "전광판"에 띄우기
                string serverResponse = www.downloadHandler.text;
                if (statusText) statusText.text = serverResponse;
            }
            else
            {
                // 실패
                if (statusText) statusText.text = "에러: " + www.error;
            }
        }
    }

    // --- (이 아래는 건드리지 마세요!) ---
    // 유니티 소리를 WAV 파일로 바꿔주는 마법의 함수
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