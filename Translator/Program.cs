using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Globalization;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;

namespace Translator
{
    class LanguageData
    {
        public string key;
        public string value;
        public bool voice;
    }

    class Language
    {
        public string name;
        public List<LanguageData> strings;

        public Language()
        {
            strings = new List<LanguageData>();
        }
    }

    //https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/quickstart-dotnet-text-to-speech
    public class Authentication
    {
        private string subscriptionKey;
        private string tokenFetchUri;

        public Authentication(string tokenFetchUri, string subscriptionKey)
        {
            if (string.IsNullOrWhiteSpace(tokenFetchUri))
            {
                throw new ArgumentNullException(nameof(tokenFetchUri));
            }
            if (string.IsNullOrWhiteSpace(subscriptionKey))
            {
                throw new ArgumentNullException(nameof(subscriptionKey));
            }
            this.tokenFetchUri = tokenFetchUri;
            this.subscriptionKey = subscriptionKey;
        }

        public async Task<string> FetchTokenAsync()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", this.subscriptionKey);
                UriBuilder uriBuilder = new UriBuilder(this.tokenFetchUri);

                var result = await client.PostAsync(uriBuilder.Uri.AbsoluteUri, null).ConfigureAwait(false);
                return await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }
    }

    class Program
    {
        static int translatetimeout = 2000; //pause for translation, and to throttle our requests to the TTS server
        static int voicetimeout = 5000;
        static string accessToken;

        static readonly Dictionary<string, string> voices = new Dictionary<string, string>()
        {
            {"en", "Microsoft Server Speech Text to Speech Voice (en-US, ZiraRUS)" },
            {"es", "Microsoft Server Speech Text to Speech Voice (es-MX, HildaRUS)" },
            {"fr", "Microsoft Server Speech Text to Speech Voice (fr-FR, HortenseRUS)" },
            {"de", "Microsoft Server Speech Text to Speech Voice (de-DE, HeddaRUS)" },
            {"ja", "Microsoft Server Speech Text to Speech Voice (ja-JP, HarukaRUS)" },
            {"it", "Microsoft Server Speech Text to Speech Voice (it-IT, LuciaRUS)" },
            {"ko", "Microsoft Server Speech Text to Speech Voice (ko-KR, HeamiRUS)" },
            {"ru", "Microsoft Server Speech Text to Speech Voice (ru-RU, EkaterinaRUS)" },
            {"zh-CN", "Microsoft Server Speech Text to Speech Voice (zh-CN, HuihuiRUS)" }
        };

        static async Task Main(string[] args)
        {
            // If your resource isn't in WEST US, change the endpoint

            var jsonData = File.ReadAllText(@"C:\Users\tucke\source\biome\Cell Matching\Assets\Resources\Localization\english.json");
            var json = JsonConvert.DeserializeObject<Language>(jsonData);

            ////First get all the speech for English
            //if (!Directory.Exists("english"))
            //    Directory.CreateDirectory("english");
            //int i = 0;
            //foreach (var kv in json.strings)
            //{
            //    if (i%10 == 0)
            //    {
            //        var ret = await UpdateAccessCode((i / 10) % 2 == 0);
            //        if (!ret) return;
            //        i++;
            //    }
            //    Console.WriteLine(DateTime.Now.ToLongTimeString() + ": " + kv.value);
            //    await GetSpeech("english", "en-US", kv.key, kv.value);
            //    Thread.Sleep(timeout);
            //    i++;
            //}
            //Console.WriteLine("done reading English");

            var langs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("spanish", "es"),
                new KeyValuePair<string, string>("french", "fr"),
                new KeyValuePair<string, string>("german", "de"),
                new KeyValuePair<string, string>("japanese", "ja"),
                new KeyValuePair<string, string>("italian", "it"),
                new KeyValuePair<string, string>("korean", "ko"),
                new KeyValuePair<string, string>("russian", "ru")
            };
            foreach (var l in langs)
                await TranslateMicrosoft(l.Key, l.Value, json.strings);
            langs = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("chinese", "zh-CN"),
            };
            foreach (var l in langs)
                await TranslateGoogle(l.Key, l.Value, json.strings);
        }

        static async Task<bool> UpdateAccessCode(bool key1)
        {
            // Add your subscription key here
            var key1CognitiveServices = "6327355d74fc439183d2077070329328";
            var key2CognitiveServices = "e94a19d9accd4de0805a19a3e3ffcdb4";

            //Alternate between keys for each of the 10 requests
            var auth = new Authentication("https://westus.api.cognitive.microsoft.com/sts/v1.0/issueToken",
                (key1 ? key1CognitiveServices : key2CognitiveServices));
            try
            {
                accessToken = await auth.FetchTokenAsync().ConfigureAwait(false);
                Console.WriteLine("Successfully obtained an access token. \n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to obtain an access token.");
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        static async Task TranslateGoogle(string language, string lang_code, List<LanguageData> lps)
        {
            if (!Directory.Exists(language))
                Directory.CreateDirectory(language);
            var destLang = new Language();
            var textinfo = new CultureInfo(lang_code, false);

            using (var driver = new ChromeDriver())
            {
                var url = "https://translate.google.com/#view=home&op=translate&sl=en&tl=" + lang_code + "&text=hello";
                driver.Navigate().GoToUrl(url);
                var builder = new Actions(driver);
                var clearContents = builder.KeyDown(Keys.Control)
                                            .SendKeys("a")
                                            .KeyUp(Keys.Control)
                                            .SendKeys(Keys.Delete);
                var inText = driver.FindElementById("source");

                //First get the language value
                inText.Click();
                clearContents.Perform();
                inText.SendKeys(language);
                Thread.Sleep(translatetimeout);
                var outText = driver.FindElementByCssSelector("body > div.frame > div.page.tlid-homepage.homepage.translate-text > div.homepage-content-wrap > div.tlid-source-target.main-header > div.source-target-row > div.tlid-results-container.results-container > div.tlid-result.result-dict-wrapper > div.result.tlid-copy-target > div.text-wrap.tlid-copy-target > div > span.tlid-translation.translation > span");
                destLang.name = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(outText.Text.ToLower());
                Console.WriteLine(DateTime.Now.ToLongTimeString() + ": " + destLang.name);

                //Now translate the data
                var i = 0;
                foreach (var lp in lps)
                {
                    if (i % 10 == 0)
                    {
                        var ret = await UpdateAccessCode((i / 10) % 2 == 0);
                        if (!ret) return;
                        i++;
                    }

                    inText.Click();
                    clearContents.Perform();
                    inText.SendKeys(lp.value);
                    Thread.Sleep(translatetimeout);
                    var lpOut = new LanguageData();
                    lpOut.key = lp.key;
                    lpOut.value = lp.value;
                    lpOut.voice = lp.voice;
                    try
                    {
                        //This element goes stale when new text is written
                        outText = driver.FindElementByCssSelector("body > div.frame > div.page.tlid-homepage.homepage.translate-text > div.homepage-content-wrap > div.tlid-source-target.main-header > div.source-target-row > div.tlid-results-container.results-container > div.tlid-result.result-dict-wrapper > div.result.tlid-copy-target > div.text-wrap.tlid-copy-target > div > span.tlid-translation.translation > span");
                        if (outText.Text.Split(' ').Length > 5)
                            lpOut.value = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(outText.Text.ToLower());
                        else
                            lpOut.value = outText.Text;
                    }
                    catch
                    {
                        //If this fails, go with the original text
                    }
                    Console.WriteLine("{0} -> {1}", lp.value, lpOut.value);
                    destLang.strings.Add(lpOut);

                    if (lp.voice)
                        await GetSpeech(language, lang_code, lpOut.key, lpOut.value);
                }

                File.WriteAllText(language + ".json", JsonConvert.SerializeObject(destLang));
            }
        }

        static async Task TranslateMicrosoft(string language, string lang_code, List<LanguageData> lps)
        {
            if (!Directory.Exists(language))
                Directory.CreateDirectory(language);

            var destLang = new Language();

            using (var driver = new ChromeDriver())
            {
                var url = "https://www.bing.com/translator?ref=TThis&&text=&from=en&to=" + lang_code;
                driver.Navigate().GoToUrl(url);
                var builder = new Actions(driver);
                var clearContents = builder.KeyDown(Keys.Control)
                                            .SendKeys("a")
                                            .KeyUp(Keys.Control)
                                            .SendKeys(Keys.Delete);
                var inText = driver.FindElementById("t_sv");
                var outText = driver.FindElementById("t_tv");

                //First get the language value
                inText.Click();
                clearContents.Perform();
                inText.SendKeys(language);
                Thread.Sleep(translatetimeout);
                destLang.name = outText.GetAttribute("value");
                Console.WriteLine(DateTime.Now.ToLongTimeString() + ": " + destLang.name);

                //Now translate the data
                var i = 0;
                foreach (var lp in lps) {
                    if (i % 10 == 0)
                    {
                        var ret = await UpdateAccessCode((i / 10) % 2 == 0);
                        if (!ret) return;
                        i++;
                    }

                    inText.Click();
                    clearContents.Perform();
                    inText.SendKeys(lp.value);
                    Thread.Sleep(translatetimeout);
                    var lpOut = new LanguageData();
                    lpOut.key = lp.key;
                    lpOut.voice = lp.voice;
                    if (outText.GetAttribute("value").Split(' ').Length < 5)
                        lpOut.value = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(outText.GetAttribute("value").ToLower());
                    else
                        lpOut.value = outText.GetAttribute("value");
                    destLang.strings.Add(lpOut);
                    Console.WriteLine(DateTime.Now.ToLongTimeString() + ": " + lpOut.value);

                    if (lp.voice)
                        await GetSpeech(language, lang_code, lpOut.key, lpOut.value);
                }

                File.WriteAllText(language + ".json", JsonConvert.SerializeObject(destLang));
            }
        }

        //https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/quickstart-dotnet-text-to-speech
        static async Task<bool> GetSpeech(string language, string lang_code, string key, string text)
        {
            Thread.Sleep(voicetimeout);

            var host = "https://westus.tts.speech.microsoft.com/cognitiveservices/v1";
            var body = @"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='" + lang_code + "'> " +
              "<voice name='" + voices[lang_code] + "'>" + text + "</voice></speak>";

            int retry = 3;
            while (retry > 0) {
                using (var client = new HttpClient())
                {
                    using (var request = new HttpRequestMessage())
                    {
                        // Set the HTTP method
                        request.Method = HttpMethod.Post;
                        // Construct the URI
                        request.RequestUri = new Uri(host);
                        // Set the content type header
                        request.Content = new StringContent(body, Encoding.UTF8, "application/ssml+xml");
                        // Set additional header, such as Authorization and User-Agent
                        request.Headers.Add("Authorization", "Bearer " + accessToken);
                        //request.Headers.Add("Connection", "Keep-Alive");
                        // Update your resource name
                        request.Headers.Add("User-Agent", "biome");
                        request.Headers.Add("X-Microsoft-OutputFormat", "riff-24khz-16bit-mono-pcm");

                        try
                        {
                            // Create a request
                            using (var response = await client.SendAsync(request).ConfigureAwait(false))
                            {
                                response.EnsureSuccessStatusCode();
                                // Asynchronously read the response
                                using (var dataStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                                {
                                    using (var fileStream = new FileStream(string.Format("{0}/{1}.wav", language, key), FileMode.Create, FileAccess.Write, FileShare.Write))
                                    {
                                        await dataStream.CopyToAsync(fileStream).ConfigureAwait(false);
                                        fileStream.Close();
                                    }
                                }
                            }
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(text + " --> " + ex.Message);
                            if (retry == 0)
                                return false;
                            else
                            {
                                //Try sleeping for a longer time
                                Thread.Sleep(20000 * (4-retry));
                                retry--;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
