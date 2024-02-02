using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Recorder
{
    /// <summary>
    /// Add this component to a GameObject to Record Mic Input 
    /// </summary>
    //[AddComponentMenu("AhmedSchrute/Recorder")]
    [RequireComponent(typeof(AudioSource))]
    public class Recorder : MonoBehaviour
    {
        #region Constants &  Static Variables
        /// <summary>
        /// Audio Source to store Microphone Input, An AudioSource Component is required by default
        /// </summary>
        static AudioSource audioSource;
        /// <summary>
        /// The samples are floats ranging from -1.0f to 1.0f, representing the data in the audio clip
        /// </summary>
        static float[] samplesData;
        /// <summary>
        /// WAV file header size
        /// </summary>
        const int HEADER_SIZE = 44;

        #endregion

        #region Editor Exposed Variables

        /// <summary>
        /// Set a keyboard key for saving the Audio File
        /// </summary>
        [Tooltip("Set a keyboard key for saving the Audio File")]
        public KeyCode keyCode;
        /// <summary>
        /// Set a Button to trigger writing the WAV file and Saving the Audio 
        /// </summary>
        public Button SaveButton;
        /// <summary>
        /// What should the saved file name be, the file will be saved in Streaming Assets Directory
        /// </summary>
        [Tooltip("What should the saved file name be, the file will be saved in Streaming Assets Directory, Don't add .wav at the end")]
        public string fileName = "test";


        [SerializeField] private int _sampleWindow = 64;
        [SerializeField] private float _lowThreshold;
        [SerializeField] private float _highThreshold;

        private AudioClip _microphoneClip;
        private string _microphoneName;
        private bool _listening;
        private float _currentLoudness;

        public float lowThreshold => _lowThreshold;
        public float highThreshold => _highThreshold;
        public bool isListening => _listening;
        public float currentLoudness => _currentLoudness;

        #endregion

        #region MonoBehaviour Callbacks

        void Awake()
        {
            _microphoneName = Microphone.devices[0];
            _microphoneClip = Microphone.Start(_microphoneName, true, 20, AudioSettings.outputSampleRate);
            if (_microphoneClip != null)
                _listening = true;
            else
                this.enabled = false;
        }

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            //audioSource.clip = Microphone.Start(Microphone.devices[0], true, 10, 44100);
            //audioSource.Play();
            //if (SaveButton == null)
            //{
            //    return;
            //}
            SaveButton.onClick.AddListener(() =>
            {
                Save();
            });
        }

        bool isSpeaking = true;
        private void Update()
        {
            //if (Input.GetKeyDown(keyCode))
            //{
            //    Save();
            //}

            _currentLoudness = GetLoudnessFromAudioClip(Microphone.GetPosition(_microphoneName), _microphoneClip, _sampleWindow);
            if (_currentLoudness < _lowThreshold)
            {
                Debug.Log("Not Speaking");
            }
            else if (_currentLoudness > _highThreshold)
            {
                Debug.Log("Speaking");
                if (isSpeaking)
                RecordSpeaking();
            }
        }

        void RecordSpeaking()
        {
            Microphone.End(_microphoneName);
            audioSource.clip = Microphone.Start(_microphoneName, true, 5, AudioSettings.outputSampleRate);
            isSpeaking = false;
            Invoke("Save", 5.2f);
        }

        public static float GetLoudnessFromAudioClip(int clipPosition, AudioClip clip, int sampleWindow)
        {
            var startPosition = clipPosition - sampleWindow;
            if (startPosition < 0) return 0;

            var waveData = new float[sampleWindow];
            clip.GetData(waveData, startPosition);

            //compute loudness
            var totalLoudness = 0f;

            for (var i = 0; i < sampleWindow; ++i)
                totalLoudness += Mathf.Abs(waveData[i]);

            return totalLoudness / sampleWindow;
        }

        #endregion

        #region Recorder Functions

        public void Save()
        {

            while (!(Microphone.GetPosition(null) > 0)) { }
            samplesData = new float[audioSource.clip.samples * audioSource.clip.channels];
            audioSource.clip.GetData(samplesData, 0);
            string filePath = Path.Combine(Application.streamingAssetsPath, fileName + ".wav");
            Debug.Log(filePath);
            // Delete the file if it exists.
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            try
            {
                WriteWAVFile(audioSource.clip, filePath);
                Debug.Log("File Saved Successfully at StreamingAssets/" + fileName + ".wav");
            }
            catch (DirectoryNotFoundException)
            {
                Debug.LogError("Please, Create a StreamingAssets Directory in the Assets Folder");
            }
           
        }

        public static byte[] ConvertWAVtoByteArray(string filePath)
        {
            //Open the stream and read it back.
            byte[] bytes = new byte[audioSource.clip.samples + HEADER_SIZE];
            using (FileStream fs = File.OpenRead(filePath))
            {
                fs.Read(bytes, 0, bytes.Length);
            }
            return bytes;
        }

        // WAV file format from http://soundfile.sapp.org/doc/WaveFormat/
        static void WriteWAVFile(AudioClip clip, string filePath)
        {
            float[] clipData = new float[clip.samples];

            //Create the file.
            using (Stream fs = File.Create(filePath))
            {
                int frequency = clip.frequency;
                int numOfChannels = clip.channels;
                int samples = clip.samples;
                fs.Seek(0, SeekOrigin.Begin);

                //Header

                // Chunk ID
                byte[] riff = Encoding.ASCII.GetBytes("RIFF");
                fs.Write(riff, 0, 4);

                // ChunkSize
                byte[] chunkSize = BitConverter.GetBytes((HEADER_SIZE + clipData.Length) - 8);
                fs.Write(chunkSize, 0, 4);

                // Format
                byte[] wave = Encoding.ASCII.GetBytes("WAVE");
                fs.Write(wave, 0, 4);

                // Subchunk1ID
                byte[] fmt = Encoding.ASCII.GetBytes("fmt ");
                fs.Write(fmt, 0, 4);

                // Subchunk1Size
                byte[] subChunk1 = BitConverter.GetBytes(16);
                fs.Write(subChunk1, 0, 4);

                // AudioFormat
                byte[] audioFormat = BitConverter.GetBytes(1);
                fs.Write(audioFormat, 0, 2);

                // NumChannels
                byte[] numChannels = BitConverter.GetBytes(numOfChannels);
                fs.Write(numChannels, 0, 2);

                // SampleRate
                byte[] sampleRate = BitConverter.GetBytes(frequency);
                fs.Write(sampleRate, 0, 4);

                // ByteRate
                byte[] byteRate = BitConverter.GetBytes(frequency * numOfChannels * 2); // sampleRate * bytesPerSample*number of channels, here 44100*2*2
                fs.Write(byteRate, 0, 4);

                // BlockAlign
                ushort blockAlign = (ushort)(numOfChannels * 2);
                fs.Write(BitConverter.GetBytes(blockAlign), 0, 2);

                // BitsPerSample
                ushort bps = 16;
                byte[] bitsPerSample = BitConverter.GetBytes(bps);
                fs.Write(bitsPerSample, 0, 2);

                // Subchunk2ID
                byte[] datastring = Encoding.ASCII.GetBytes("data");
                fs.Write(datastring, 0, 4);

                // Subchunk2Size
                byte[] subChunk2 = BitConverter.GetBytes(samples * numOfChannels * 2);
                fs.Write(subChunk2, 0, 4);

                // Data

                clip.GetData(clipData, 0);
                short[] intData = new short[clipData.Length];
                byte[] bytesData = new byte[clipData.Length * 2];

                int convertionFactor = 32767;

                for (int i = 0; i < clipData.Length; i++)
                {
                    intData[i] = (short)(clipData[i] * convertionFactor);
                    byte[] byteArr = new byte[2];
                    byteArr = BitConverter.GetBytes(intData[i]);
                    byteArr.CopyTo(bytesData, i * 2);
                }
                
                fs.Write(bytesData, 0, bytesData.Length);
            }
        }

        #endregion
    }
}