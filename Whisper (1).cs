using System;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ElevenLabs;
using static UnityEngine.XR.ARSubsystems.XRCpuImage;
using System.Linq;

namespace OpenAI
{
    [System.Serializable]
    public class JsonResponse
    {
        public JsonResponseChoices[] choices;
    }

    [System.Serializable]
    public class JsonResponseChoices
    {
        public JsonResponseMessage message;
    }

    [System.Serializable]
    public class JsonResponseMessage
    {
        public string content;
    }
    public class Whisper : MonoBehaviour
    {
        [SerializeField] private Button recordButton;
        [SerializeField] private Image progressBar;
        [SerializeField] private Text message;
        [SerializeField] private Dropdown dropdown;
        [SerializeField] private Button endRecording;
        
        private readonly string fileName = "output.wav";
        private readonly int duration = 3;
        
        private AudioClip clip;
        private bool isRecording;
        private float time;
        private OpenAIApi openai = new OpenAIApi("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");

        public string myPrompt;
        public AudioSource audioSource;

        private void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            //AudioSource audioSource = gameObject.AddComponent<AudioSource>();
            foreach (var device in Microphone.devices)
            {
                dropdown.options.Add(new Dropdown.OptionData(device));
            }
            recordButton.onClick.AddListener(StartRecording);
            dropdown.onValueChanged.AddListener(ChangeMicrophone);
            endRecording.enabled = false;
            endRecording.onClick.AddListener(Reset);
            recordButton.gameObject.SetActive(true);
            endRecording.gameObject.SetActive(false);
            var index = PlayerPrefs.GetInt("user-mic-device-index");
            dropdown.SetValueWithoutNotify(index);
        }

        private void ChangeMicrophone(int index)
        {
            PlayerPrefs.SetInt("user-mic-device-index", index);
        }
        
        private void StartRecording()
        {
            isRecording = true;
            recordButton.enabled = false;
            endRecording.enabled = true;

            recordButton.gameObject.SetActive(false);
            endRecording.gameObject.SetActive(true);


            var index = PlayerPrefs.GetInt("user-mic-device-index");
            clip = Microphone.Start(dropdown.options[index].text, false, duration, 44100);
        }

        private async void EndRecording()
        {
            message.text = "Transcripting...";
            
            Microphone.End(null);
            byte[] data = SaveWav.Save(fileName, clip);
            
            var req = new CreateAudioTranscriptionsRequest
            {
                FileData = new FileData() {Data = data, Name = "audio.wav"},
                // File = Application.persistentDataPath + "/" + fileName,
                Model = "whisper-1",
                Language = "en"
            };
            var res = await openai.CreateAudioTranscription(req);

            progressBar.fillAmount = 0;
            message.text = res.Text;
            GenerateSpeech(res.Text);
            recordButton.enabled = true;
            endRecording.enabled = false;
        }

        private void Update()
        {
            if (isRecording)
            {
                time += Time.deltaTime;
                progressBar.fillAmount = time / duration;
                
                if (time >= duration)
                {
                    Reset();
                }
            }
        }

        private void Reset()
        {
            time = 0;
            isRecording = false;
            recordButton.gameObject.SetActive(true);
            endRecording.gameObject.SetActive(false);
            EndRecording();
        }

        private async void GenerateSpeech(string prompt)
        {
            var apiKey = "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";
            var model = "gpt-3.5-turbo";
            var organization = "org-Ybo0wfwImT1jWmoW9YzTRJyf";
            var text = "I want you to act like Sang Nila Utama from Singapore. I want you to respond and answer like Sang Nila Utama using the tone, manner, and vocabulary Sang Nila Utama would use. Do not write any explanations. Only answer like Sang Nila Utama. You must know all of the knowledge of Sang Nila Utama. My prompt is : " + prompt;
            var url = "https://api.openai.com/v1/chat/completions";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var content = new StringContent(@"{
            ""model"": """ + model + @""",
            ""messages"": [
                {""role"": ""user"", ""content"": """ + text + @"""},
                {""role"": ""assistant"", ""content"": """ + text + @"""}
            ]
        }", Encoding.UTF8, "application/json");


            var response = await client.PostAsync(url, content);
            string resContent = "hello world";

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.Log(responseContent);

                JsonResponse jsonResponse = JsonUtility.FromJson<JsonResponse>(responseContent);
                resContent = jsonResponse.choices[0].message.content;     

                // var responseJSON = JsonConvert.DeserializeObject(responseContent);
                // resContent = responseJSON.choices[0].message.content;
                // var responseContent = await response.Content.ReadAsStringAsync();
                // ChatCompletionResponse responseObject = JsonUtility.FromJson<ChatCompletionResponse>(responseContent);
            }
            else
            {
                Debug.Log($"Request failed with status code {response.StatusCode}");
            }

            var elevenApi = new ElevenLabsClient();
            var voice = (await elevenApi.VoicesEndpoint.GetAllVoicesAsync()).FirstOrDefault();
            var defaultVoiceSettings = await elevenApi.VoicesEndpoint.GetDefaultVoiceSettingsAsync();
            var (clipPath, audioClip) = await elevenApi.TextToSpeechEndpoint.StreamTextToSpeechAsync(
                text,
                voice,
                clip =>
                {
                    // Event raised as soon as the clip has loaded enough data to play.
                    // May not provide or play full clip until Unity bug is addressed.
                    audioSource.PlayOneShot(clip);
                },
                defaultVoiceSettings);
            Debug.Log(clipPath);


            //var elevenApiEndpoint = "https://api.elevenlabs.io/v1/text-to-speech/21m00Tcm4TlvDq8ikWAM";
            //var elevenApiKey = "035b5d6fc27d84a11acb868afb6b5d6c";
            //var audioOutputFile = "output.mp3";

            //var elevenApiHttpClient = new HttpClient();
            //elevenApiHttpClient.DefaultRequestHeaders.Add("Accept", "audio/mpeg");
            //elevenApiHttpClient.DefaultRequestHeaders.Add("xi-api-key", elevenApiKey);

            //var elevenApiData = new
            //{
            //    text = resContent,
            //    model_id = "eleven_monolingual_v1",
            //    voice_settings = new
            //    {
            //        stability = 0.5,
            //        similarity_boost = 0.5
            //    }
            //};


            //var elevenApiJsonData = JsonConvert.SerializeObject(elevenApiData);
            //var elevenApiContent = new StringContent(elevenApiJsonData, Encoding.UTF8, "application/json");

            //var elevenApiResponse = await elevenApiHttpClient.PostAsync(elevenApiEndpoint, elevenApiContent);
            //var audioBytes = await elevenApiResponse.Content.ReadAsByteArrayAsync();

            //Debug.Log(audioBytes.Length);

            //float[] floatData = new float[audioBytes.Length / 2]; // Assuming 16-bit audio data

            // Convert bytes to floats
            //for (int i = 0; i < floatData.Length; i++)
            //{
            //    short value = (short)((audioBytes[i * 2 + 1] << 8) | audioBytes[i * 2]);
            //    floatData[i] = value / 32768f; // Normalize to range -1.0 to 1.0
            //}

            // await File.WriteAllBytesAsync(audioOutputFile, audioBytes);

            //Debug.Log("Conversion completed successfully!");

            //AudioClip audioClip = AudioClip.Create("AudioClip", floatData.Length, 1, 44100, false);
            //audioClip.SetData(floatData, 0);  

            //AudioSource audioSource = GetComponent<AudioSource>();
            //audioSource.clip = audioClip;  

            //audioSource.Play();

        }
    }
}
