using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Newtonsoft.Json;

namespace SchoolyConnect
{

    

    public class _SchoolRequest
    {
        public string solution_id { get; set; }
        public string name { get; set; }
        public string school_name { get; set; }
        public string status { get; set; }
        public bool kill { get; set; }

        string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        public string AsString()
        {
            return String.Format("solution_id={0},name={1},school_name={2},status={3},kill={4}",
                solution_id, Reverse(name), Reverse(school_name), status, kill);
        }

    }

    class Connect
    {
        private string host { get; set; }
        public bool devMode { get; set; }
        public List<_SchoolRequest> requests;

        public Connect()
        {
            host = "https://my-bg.schooly.co.il/schedule";
        }

        /*
         public Connect(bool isDevMode)
        {
            host = "http://dev.schooly.co.il:3000/schedule";
        }
        */


        public string Http(string url, string postData = null)
        {
            WebRequest request = WebRequest.Create(String.Format("{0}/{1}", host, url));
            if (postData != null)
            {
                request.Method = "POST";
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                request.ContentType = "application/json";
                request.ContentLength = byteArray.Length;
                Stream reqStream = request.GetRequestStream();
                reqStream.Write(byteArray, 0, byteArray.Length);
                reqStream.Close();
            }
            else
            {
                request.Method = "GET";
            }

            WebResponse response = request.GetResponse();
            Stream resStream = response.GetResponseStream();
            StreamReader reader = new StreamReader(resStream);
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            resStream.Close();
            response.Close();

            return responseFromServer;
        }

        public string GetRequestData(string solution_id)
        {
            return Http(String.Format("solution/{0}", solution_id));
        }

        public void PollRequests()
        {
            string json = Http("solution/list");
            requests = JsonConvert.DeserializeObject<List<_SchoolRequest>>(json);
        }


        public void SetStatus (string solution_id, string status)
        {
            Http(String.Format("solution/{0}/status/{1}", solution_id, status));
        }
    }
}
