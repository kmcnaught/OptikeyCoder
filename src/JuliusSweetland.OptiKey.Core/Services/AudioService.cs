// Copyright (c) 2022 OPTIKEY LTD (UK company number 11854839) - All Rights Reserved
using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows;
using System.Net;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.Services.Audio;
using log4net;
using Microsoft.Win32.SafeHandles;
using Un4seen.Bass;
using SpeechLib;
using System.Windows.Controls;

namespace JuliusSweetland.OptiKey.Services
{
    public class AudioService : IAudioService
    {
        #region Constants

        private const string BassRegistrationEmail = "optikeyfeedback@gmail.com";
        private const string BassRegistrationKey = "2X24252025152222";

        #endregion

        #region Private Member Vars

        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private readonly SpeechSynthesizer speechSynthesiser;
        private SpVoice legacySpeechSynthesiser;
        private readonly SoundPlayerEx maryTtsPlayer;

        private readonly object speakCompletedLock = new object();
        private EventHandler<SpeakCompletedEventArgs> onSpeakCompleted;
        private WaitOrTimerCallback legacySpeakCompleted;
        private EventHandler onMaryTtsSpeakCompleted;

        private readonly HashSet<string> useLegacyMicrosoftSpeechForVoices = new HashSet<string>();
        private readonly Dictionary<string, int> legacyMicrosoftSpeechVoiceToTokenIndexLookup = new Dictionary<string, int>();

        #endregion

        #region Events

        public event EventHandler<Exception> Error;

        #endregion

        #region Ctor

        public AudioService()
        {
            speechSynthesiser = new SpeechSynthesizer();
            maryTtsPlayer = new SoundPlayerEx();
            BassNet.Registration(BassRegistrationEmail, BassRegistrationKey);
            Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            Application.Current.Exit += (sender, args) => Bass.BASS_Free();
        }

        #endregion

        #region Public methods
        
        /// <summary>
        /// Start speaking the supplied text, or cancel the in-progress speech
        /// </summary>
        /// <returns>Indication of whether speech is now in progress</returns>
        public bool SpeakNewOrInterruptCurrentSpeech(string textToSpeak, Action onComplete, int? volume = null, int? rate = null, string voice = null, bool isIPA = false, bool slowerIPA = false)
        {
            Log.Info("SpeakNewOrInterruptCurrentSpeech called");

            if (string.IsNullOrEmpty(textToSpeak)) return false;

            try
            {
                lock (speakCompletedLock)
                {
                    if (onSpeakCompleted == null && legacySpeakCompleted == null && onMaryTtsSpeakCompleted == null)
                    {                        
                        Speak(textToSpeak, onComplete, volume, rate, voice, isIPA, slowerIPA);
                        return true;
                    }
                    CancelSpeech(voice);
                }
            }
            catch (Exception exception)
            {
                PublishError(this, exception);
            }
            return false;
        }

        public List<string> GetAvailableVoices()
        {
            Log.Info("GetAvailableVoices called");
            var availableVoices = new SpeechSynthesizer().GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToList();

            Log.InfoFormat("GetAvailableVoices returing {0} voices", availableVoices.Count);

            return availableVoices;
        }

        public List<string> GetAvailableMaryTTSVoices()
        {
            var availableVoices = new List<string>();

            if (Settings.Default.MaryTTSEnabled)
            {
                Log.Info("GetAvailableMaryTTSVoices called");

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:59125/voices");

                //Set some reasonable limits on resources used by this request
                request.MaximumAutomaticRedirections = 4;
                request.MaximumResponseHeadersLength = 4;

                //Set credentials to use for this request.
                request.Credentials = CredentialCache.DefaultCredentials;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                //Get the stream associated with the response.
                Stream receiveStream = response.GetResponseStream();

                //Pipes the stream to a higher level stream reader with the required encoding format. 
                StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8);
                string responseText = readStream.ReadToEnd();
                availableVoices = responseText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

                Log.InfoFormat("GetAvailableMaryTTSVoices returing {0} voices", availableVoices.Count);
            }

            return availableVoices;
        }

        public void PlaySound(string file, int volume)
        {
            Log.InfoFormat("Playing sound '{0}' at volume '{1}'", file, volume);
            if (string.IsNullOrEmpty(file)) return;

            try
            {
                // create a stream channel from a file 
                int stream = Bass.BASS_StreamCreateFile(file, 0L, 0L, BASSFlag.BASS_DEFAULT | BASSFlag.BASS_STREAM_AUTOFREE);
                if (stream != 0)
                {
                    Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, (volume/100f));
                    Bass.BASS_ChannelPlay(stream, false);
                }
                else
                {
                    BASSError err = Bass.BASS_ErrorGetCode();
                    Log.ErrorFormat($"Error code:{ err } creating audio stream");
                    throw new ApplicationException(string.Format(Resources.BASS_UNABLE_TO_CREATE_STREAM_FROM_FILE, file));
                }
            }
            catch (Exception exception)
            {
                //Don't publish error - the error handler tries to play a sound file which could loop us right back here
                Log.Error("Exception encountered within the AudioService", exception);
            }
        }

        private void PublishError(object sender, Exception ex)
        {
            Log.Error("Publishing Error event (if there are any listeners)", ex);
            if (Error != null)
            {
                Error(sender, ex);
            }
        }

        #endregion

        #region Private methods

        private void CancelSpeech(string voice = null)
        {
            var voiceToUse = voice ?? Settings.Default.SpeechVoice;
            Log.Info($"Cancelling all speech (voice = {voiceToUse})");
            if (useLegacyMicrosoftSpeechForVoices.Contains(voiceToUse))
            {
                Log.Warn("Cancel speech attempted, but using legacy speech synthesiser which does not support this.");
                return;
            }

            lock (speakCompletedLock)
            {
                if (onSpeakCompleted != null)
                {
                    speechSynthesiser.SpeakCompleted -= onSpeakCompleted;
                    onSpeakCompleted = null;
                }
                if (onMaryTtsSpeakCompleted != null)
                {
                    maryTtsPlayer.SoundFinished -= onMaryTtsSpeakCompleted;
                    onMaryTtsSpeakCompleted = null;
                }
                speechSynthesiser.SpeakAsyncCancelAll();
                maryTtsPlayer.Stop();
            }
        }

        private async void Speak(string textToSpeak, Action onComplete, int? volume = null, int? rate = null, string voice = null, bool isIPA = false, bool slowerIPA = false)
        {
            Log.InfoFormat("Speaking '{0}' with volume '{1}', rate '{2}' and voice '{3}' and delay {4}ms", textToSpeak, volume, rate, 
                !Settings.Default.MaryTTSEnabled ? voice : Settings.Default.MaryTTSVoice, Settings.Default.SpeechDelay);

            if (string.IsNullOrEmpty(textToSpeak)) return;

            if (Settings.Default.SpeechDelay > 0)
            {
                await Task.Delay(Settings.Default.SpeechDelay);
            }

            if (isIPA)
            {
                PronouncePhonemesWithMicrosoftSpeechLibrary(textToSpeak, onComplete, volume, rate, voice, slowerIPA);             
            }
            else if (Settings.Default.MaryTTSEnabled)
            {
                SpeakWithMaryTTS(textToSpeak, onComplete, volume, rate);
            }
            else
            {
                SpeakWithMicrosoftSpeechLibrary(textToSpeak, onComplete, volume, rate, voice);
            }
        }

        private SpeechSynthesizer synthesiserForIPA;
        private void SetupIPASynth(string voice)
        {
            // TODO: how do we reconcile this with the standard synth's voice?
            if (synthesiserForIPA == null)
            {
                synthesiserForIPA = new SpeechSynthesizer();

                var voiceToUse = voice ?? Settings.Default.SpeechVoice;

                if (String.IsNullOrEmpty(voiceToUse))
                {
                    VoiceInfo preferredVoice;

                    // Get best fit voice                
                    var voices = synthesiserForIPA.GetInstalledVoices().Select(v => v.VoiceInfo).ToList();

                    // Try to find an en-GB female voice first
                    preferredVoice = voices.FirstOrDefault(v => v.Culture.Name == "en-GB" && v.Gender == VoiceGender.Female);

                    // If not found, try to find any en-GB voice
                    if (preferredVoice == null)
                        preferredVoice = voices.FirstOrDefault(v => v.Culture.Name == "en-GB");

                    // If still not found, try to find any en-* female voice
                    if (preferredVoice == null)
                        preferredVoice = voices.FirstOrDefault(v => v.Culture.Name.StartsWith("en") && v.Gender == VoiceGender.Female);

                    // If still not found, try to find any en-* voice
                    if (preferredVoice == null)
                        preferredVoice = voices.FirstOrDefault(v => v.Culture.Name.StartsWith("en"));

                    if (preferredVoice == null)
                    {
                        Log.Error("No suitable voice found for phonemes");
                        return;
                    }
                    else
                    {
                        voiceToUse = preferredVoice.Name;
                    }

                }

                synthesiserForIPA.SelectVoice(voiceToUse);
            }
        }

        private string GetIPAString(string ipaString, bool slower, string lang)
        {
            string str = "<speak version=\"1.0\" xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang=\"" + lang + "\">";

            if (slower)
                str += "<prosody rate=\"slow\">";

            
            foreach (string word in ipaString.Split(' '))
            {
                str += $"        <phoneme alphabet=\"ipa\" ph=\"{word}\">phonemes</phoneme>"; // ugh hack with content not matching                                                                                                  
                str += "<break time=\"300ms\"/>";
            }

            if (slower)
                str += "</prosody>";

            str += "</speak>";

            return str; 
        }

        private void PronouncePhonemesWithMicrosoftSpeechLibrary(string textToSpeak, Action onComplete, int? volume, int? rate, string voice, bool slower = false)
        {
            SetupIPASynth(voice);

            // TODO: language: should we support e.g. US english?? could look at selected voice?
            string str = GetIPAString(textToSpeak, slower, "en-GB");

            try
            {
                onSpeakCompleted = (sender, args) =>
                {
                    lock (speakCompletedLock)
                    {
                        if (onSpeakCompleted != null)
                        {
                            synthesiserForIPA.SpeakCompleted -= onSpeakCompleted;
                            onSpeakCompleted = null;
                        }

                        if (onComplete != null)
                        {
                            onComplete();
                        }
                    }
                };
                synthesiserForIPA.SpeakCompleted += onSpeakCompleted;
                synthesiserForIPA.SpeakSsmlAsync(str);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }                        
        }

        private void SpeakWithMicrosoftSpeechLibrary(string textToSpeak, Action onComplete, int? volume, int? rate, string voice)
        {

            var voiceToUse = voice ?? Settings.Default.SpeechVoice;
            if (!string.IsNullOrWhiteSpace(voiceToUse))
            {
                if (!useLegacyMicrosoftSpeechForVoices.Contains(voiceToUse))
                {
                    try
                    {
                        speechSynthesiser.SelectVoice(voiceToUse);
                        speechSynthesiser.Rate = rate ?? Settings.Default.SpeechRate;
                        speechSynthesiser.Volume = volume ?? Settings.Default.SpeechVolume;
                    }
                    catch //(Exception exception)
                    {
                        //Commenting out the raising of an error notification for now
                        //var customException = new ApplicationException(string.Format(Resources.UNABLE_TO_SET_VOICE_WARNING,
                        //    voiceToUse, voice == null ? Resources.VOICE_COMES_FROM_SETTINGS : null), exception);
                        //PublishError(this, customException);

                        Log.Warn($"Unable to speak using SpeechSynthesizer and voice '{voiceToUse}'. Switching to legacy speech mode and trying again...");
                        useLegacyMicrosoftSpeechForVoices.Add(voiceToUse);
                    }
                }

                if (useLegacyMicrosoftSpeechForVoices.Contains(voiceToUse))
                {
                    Log.Info("Attempting speech using legacy mode.");
                    try
                    {
                        if (legacySpeechSynthesiser == null)
                        {
                            //Lazy instantiate legacy speech synthesiser
                            legacySpeechSynthesiser = new SpVoice();
                        }

                        var availableVoices = legacySpeechSynthesiser.GetVoices(string.Empty, string.Empty);
                        if (legacyMicrosoftSpeechVoiceToTokenIndexLookup.ContainsKey(voiceToUse))
                        {
                            int voiceIndex = legacyMicrosoftSpeechVoiceToTokenIndexLookup[voiceToUse];
                            Log.InfoFormat($"{voiceToUse} voice token exists at index {voiceIndex}. Setting voice on legacy speech synthesiser.");
                            legacySpeechSynthesiser.Voice = availableVoices.Item(voiceIndex);
                            Log.Info("Voice token set.");
                        }
                        else
                        {
                            for (int voiceIndex = 0; voiceIndex < availableVoices.Count; voiceIndex++)
                            {
                                var voiceToken = availableVoices.Item(voiceIndex);
                                if (voiceToken.GetDescription() == voiceToUse)
                                {
                                    Log.InfoFormat($"{voiceToUse} voice token found at index {voiceIndex}. Setting voice on legacy speech synthesiser.");
                                    legacyMicrosoftSpeechVoiceToTokenIndexLookup.Add(voiceToUse, voiceIndex);
                                    legacySpeechSynthesiser.Voice = voiceToken;
                                    Log.Info("Voice token set.");
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        var customException = new ApplicationException(string.Format(Resources.UNABLE_TO_SET_VOICE_WARNING,
                            voiceToUse, voice == null ? Resources.VOICE_COMES_FROM_SETTINGS : null), exception);
                        PublishError(this, customException);
                    }
                }
            }

            // Speak

            if (!useLegacyMicrosoftSpeechForVoices.Contains(voiceToUse))
            {
                onSpeakCompleted = (sender, args) =>
                {
                    lock (speakCompletedLock)
                    {
                        if (onSpeakCompleted != null)
                        {
                            speechSynthesiser.SpeakCompleted -= onSpeakCompleted;
                            onSpeakCompleted = null;
                        }

                        if (onComplete != null)
                        {
                            onComplete();
                        }
                    }
                };
                speechSynthesiser.SpeakCompleted += onSpeakCompleted;
                speechSynthesiser.SpeakAsync(textToSpeak);
            }
            else
            {
                //Legacy speech mode
                if (legacySpeechSynthesiser != null)
                {
                    legacySpeechSynthesiser.Speak(textToSpeak, SpeechVoiceSpeakFlags.SVSFIsNotXML | SpeechVoiceSpeakFlags.SVSFlagsAsync);
                    var speechHandle = legacySpeechSynthesiser.SpeakCompleteEvent();
                    var speechHandlePtr = new IntPtr(speechHandle);
                    if (speechHandlePtr != IntPtr.Zero)
                    {
                        var autoResetEvent = new AutoResetEvent(false)
                        {
                            SafeWaitHandle = new SafeWaitHandle(speechHandlePtr, false)
                        };
                        var uiThreadDispatcher = Dispatcher.CurrentDispatcher;
                        legacySpeakCompleted = (state, timedOut) =>
                        {
                            if (onComplete != null)
                            {
                                uiThreadDispatcher.Invoke(onComplete);
                            }
                            autoResetEvent.Dispose();
                            legacySpeakCompleted = null;
                        };
                        ThreadPool.RegisterWaitForSingleObject(autoResetEvent, legacySpeakCompleted, null, 30000, true);
                    }
                }
            }
        }

        private void SpeakWithMaryTTS(string textToSpeak, Action onComplete, int? volume, int? rate)
        {
            float maryTTSRate = rate ?? Settings.Default.SpeechRate;
            float maryTTSVolume = volume ?? Settings.Default.SpeechVolume;

            maryTTSRate = (maryTTSRate + 10.0f) / 20.0f * 3.0f;
            maryTTSRate = maryTTSRate < 0.1f ? 0.1f
                : maryTTSRate > 3.0f ? 3.0f : maryTTSRate;

            maryTTSVolume = maryTTSVolume / 100.0f * 1.0f;
            maryTTSVolume = maryTTSVolume < 0.0f ? 0.0f
                : maryTTSVolume > 1.0f ? 1.0f : maryTTSVolume;

            List<string> voiceParameters = Settings.Default.MaryTTSVoice.Split(' ').ToList();

            maryTtsPlayer.SoundLocation = "http://localhost:59125/process?"
                                          + "INPUT_TYPE=TEXT&AUDIO=WAVE_FILE&"
                                          + "LOCALE=" + voiceParameters.ElementAt(1) + "&"
                                          + "VOICE=" + voiceParameters.ElementAt(0) + "&"
                                          + "INPUT_TEXT=" + textToSpeak + "&"
                                          + "OUTPUT_TYPE=AUDIO&"
                                          + "effect_Rate_selected=on&effect_Rate_parameters=durScale:"
                                          + string.Format("{0:N1}", maryTTSRate) + ";&"
                                          + "effect_Volume_selected=on&effect_Volume_parameters=amount:"
                                          + string.Format("{0:N1}", maryTTSVolume) + ";";

            onMaryTtsSpeakCompleted = (sender, args) =>
            {
                lock (speakCompletedLock)
                {
                    maryTtsPlayer.SoundFinished -= onMaryTtsSpeakCompleted;
                    onMaryTtsSpeakCompleted = null;
                    if (onComplete != null)
                    {
                        onComplete();
                    }
                }
            };

            maryTtsPlayer.SoundFinished += onMaryTtsSpeakCompleted;
#pragma warning disable 4014
            maryTtsPlayer.PlayAsync(ex => PublishError(this, ex)); //Awaitable, but we don't want to await it
#pragma warning restore 4014
        }

        #endregion
    }
}