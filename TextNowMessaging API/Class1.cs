using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Script.Serialization;

namespace TextNowMessaging_API
{
    public class TextNowClient
    {
        public string Username;
        public string Password;

        public TextNowClient()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                new System.Net.Security.RemoteCertificateValidationCallback(delegate { return true; });
        }
        public TextNowClient(string username, string password)
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                new System.Net.Security.RemoteCertificateValidationCallback(delegate { return true; });

            Username = username;
            Password = password;
        }

        public string FromName
        {
            get
            {
                return fromName.Replace("+", " ");
            }
            set
            {
                fromName = value.Replace(" ", "+");
            }
        }

        private string fromName;

        CookieContainer cookiecontainer;
        HttpClientHandler handler;
        HttpClient client;
        public void Login()
        {
            try
            {
                cookiecontainer = new CookieContainer();
                handler = new HttpClientHandler();
                handler.CookieContainer = cookiecontainer;
                client = new HttpClient(handler);

                Uri loginUri = new Uri("https://www.textnow.com/api/sessions");
                client.DefaultRequestHeaders.Host = Dns.GetHostEntry("www.textnow.com").AddressList[0].ToString();
                client.DefaultRequestHeaders.Add("Referer", "https://www.textnow.com/");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

                string loginJson = "json={\"password\":\"" + Password + "\",\"username\":\"" + Username + "\",\"remember\":0}";
                StringContent loginContent = new StringContent(loginJson, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpContent content = (HttpContent)loginContent;

                System.Threading.Tasks.Task<HttpResponseMessage> aResponse = client.PostAsync(loginUri, content);
                while (!aResponse.IsCompleted) ;
                Thread msgCheckThread = new Thread(new ThreadStart(CheckMessages));
                msgCheckThread.IsBackground = true;
                msgCheckThread.Name = "Message Checker";
                msgCheckThread.Start();

                FromName = Username;
            }
            catch
            {

            }
        }

        public void SendMessage(string to, string message, bool async = true)
        {
            try
            {
                DateTime curr = DateTime.Now;
                string date = curr.ToString("ddd") + curr.ToString("MMM") + curr.ToString("dd") + curr.ToString("yyyy") + curr.ToString("HH") + "%3A" + curr.ToString("mm") + "%3A" + curr.ToString("ss") + "+GMT-0700+(Pacific+Daylight+Time)";
                string msgJson = "json=%7B%22contact_value%22%3A%22" + to + "%22%2C%22contact_type%22%3A2%2C%22message%22%3A%22" + message + "%22%2C%22read%22%3A1%2C%22message_direction%22%3A2%2C%22message_type%22%3A1%2C%22date%22%3A%22" + date + "%22%2C%22from_name%22%3A%22" + fromName + "%22%7D";
                StringContent msgContent = new StringContent(msgJson, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpContent content = (HttpContent)msgContent;
                Uri msgUri = new Uri("https://www.textnow.com/api/users/" + Username + "/messages");
                System.Threading.Tasks.Task<HttpResponseMessage> aResponse = client.PostAsync(msgUri, content);
                if (!async)
                {
                    while (!aResponse.IsCompleted) ;
                }
            }
            catch
            {

            }
        }

        public event EventHandler<Message> OnMessage;
        private void CheckMessages()
        {
            while (true)
            {
                try
                {
                    Uri retrieveMessagesUri = new Uri("https://www.textnow.com/api/users/" + Username + "/messages?start_message_id=0&direction=future");
                    System.Threading.Tasks.Task<HttpResponseMessage> aResponse = client.GetAsync(retrieveMessagesUri);
                    System.Threading.Tasks.Task<string> result = aResponse.Result.Content.ReadAsStringAsync();

                    JavaScriptSerializer jss = new JavaScriptSerializer();
                    Dictionary<string, dynamic> dict = new Dictionary<string, dynamic>();
                    dict = jss.Deserialize<Dictionary<string, dynamic>>(result.Result);

                    long msgId;
                    string contact = "";
                    List<string> deleted = new List<string>();
                    foreach (Dictionary<string, dynamic> msg in dict["messages"])
                    {
                        contact = msg["contact_value"];
                        if (msg["message_direction"] == 1 && msg["read"] == false)
                        {
                            msgId = msg["id"];
                            Uri setReadTrueUri = new Uri("https://www.textnow.com/api/users/" + Username + "/conversations/" + contact + "?latest_message_id=" + msgId + "&http_method=PATCH");
                            string readToTrueJson = "json=%7B%22read%22%3Atrue%7D";
                            StringContent readToTrueContent = new StringContent(readToTrueJson, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                            HttpContent content = (HttpContent)readToTrueContent;

                            System.Threading.Tasks.Task<HttpResponseMessage> response = client.PostAsync(setReadTrueUri, content);
                            while (!response.IsCompleted) ;

                            OnMessage(this, new Message(contact, msg["message"]));
                        }
                        if (!deleted.Contains(contact))
                        {
                            string deleteUri = "https://www.textnow.com/api/users/" + Username + "/conversations/" + contact;
                            System.Threading.Tasks.Task<HttpResponseMessage> response = client.DeleteAsync(deleteUri);
                            while (!response.IsCompleted) ;
                            deleted.Add(contact);
                        }
                    }
                }
                catch
                {

                }
                Thread.Sleep(100);
            }
        }
    }

    public class Message
    {
        public string From;
        public string Body;

        public Message(string from, string body)
        {
            From = from;
            Body = body;
        }
    }
}
