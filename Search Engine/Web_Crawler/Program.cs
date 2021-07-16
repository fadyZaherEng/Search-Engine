using HtmlAgilityPack;
using mshtml;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using NTextCat;
using System.Text.RegularExpressions;
using Porter2StemmerStandard;

namespace Web_Crawler
{
    class Program
    {
        public static String connString = "Data Source=FADYCSASU\\SQLEXPRESS;Initial Catalog=Search_Engine;Integrated Security=True";
        public static HashSet<String> Links_Visited = new HashSet<String>();
        public static Queue<String> Links_Not_Visited = new Queue<String>();
        public static String URL;
        public static WebRequest myWebRequest;
        public static WebResponse myWebResponse;
        public static StreamReader sReader;
        public static String rString;
        public static Stream streamResponse;
        public static int numLink = 1;
        public static bool flag = true;
        public static Dictionary<String, List<int>> DocIntersection = new Dictionary<String, List<int>>();
        public static List<String> PUNCTUATION = new List<String>()
        {
        "’'" ,"()[]{}<>" , ":" ,"‒–—―" ,"…" ,"!" ,"." ,"«»" ,"-‐" ,"?" , "‘’“”",";" ,"/" ,"⁄" ,
        "·" ,"&" ,"@","*" ,"\\" ,"•" ,"^" ,"¤¢$€£¥₩₪" , "†‡" ,"°" ,"¡" ,"¿" ,"¬" ,
        "#" ,"№" ,"%‰‱" ,"¶" ,"′" ,"§" ,"~" ,"¨" ,"_" ,"|¦" ,"⁂" ,"☞" ,"∴" ,"‽" ,"※" ,"dot"
        };
        public static List<String> StopWords = new List<String>()
        {
             "help","us","i","me","my","myself","we","our","ours","ourselves","you","your","now" ,
             "yours","yourself","yourselves","he","him","his","himself","she","her","hers"       ,
             "herself","it","its","itself","they","them","their","theirs","themselves","what"    ,
             "which","who","whom","this","that","these","those","am","is","are","was","were"     ,
             "be","been","being","have","has","had","having","do","did","does","doing","a"       ,
             "an","the","and","but","if","or","because","as","until","while","of","at","by"      ,
             "for","with","about","against","between","into","through","during","before","should",
             "after","above","below","to","from","up","down","in","out","on","off","over"        ,
             "under","again","further","then","once","here","there","when","where","why","how"   ,
             "all","any","both","each","few","more","most","other","some","such","no","nor","not",
             "only","own","same","so","than","too","very","s","t","can","will","just","don"
        };

        struct TermDetails
        {
            public String doc_id;
            public String term_pos;
            public int term_freq;
        }
        public struct TermDetail
        {
            public String doc_id_term_pos;
            public String term_freq;
        }
        public struct TermQuery
        {
            public String doc_id_term_pos;
            public String term;
            public String term_freq;
        }
        public static Dictionary<String, TermDetail> TermsDataBaseSql = new Dictionary<String, TermDetail>();
        static void Main(string[] args)
        {
            Kgram kgram = new Kgram();

            Dictionary<String, TermQuery> list = new Dictionary<String, TermQuery>();
            Console.WriteLine("Enter Search Query");
            String Query = Console.ReadLine();
            //////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////
          bool f = kgram.Search_Query(Query.ToLower());
            if (!f)
            {
                Console.Write("Did You Mean : ");
                String spelling = "";
                foreach (var v in kgram.AfterKGram)
                {
                    spelling += v + " ";
                }
                Console.WriteLine(spelling);
                //   return;
                Console.WriteLine("Your Ansower");
                if (Console.ReadLine() == "yes")
                {
                    Query = spelling;
                }
            }
            /////////////////////////////////////////////////////////////////////
            //////////////////////////////////////////////////////////////////////

            bool m = false;
            if (Query.Contains("\""))
            {
                Query = Query.Replace("\"", string.Empty).Trim();
                m = true;
            }
            List<String> Terms = TokanizationMethod(Query);
            Terms = Terms.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            List<String> FinalTerms = clean(Terms);
            List<String> Clean_Query = Linquistics_Query(FinalTerms);
            foreach (var term in Clean_Query)
            {
                SqlConnection con = new SqlConnection(connString);
                string query = "select * from Inverted_Index Where Term = @T";
                con.Open();
                SqlCommand c = new SqlCommand(query, con);
                c.Parameters.AddWithValue("@T", term);
                SqlDataReader sdrDoc = c.ExecuteReader();
                while (sdrDoc.Read())
                {
                    if (!list.ContainsKey(term))
                    {
                        TermQuery obj;
                        obj.doc_id_term_pos = sdrDoc["DocIdAndPos"].ToString();
                        obj.term = term;
                        obj.term_freq = sdrDoc["TermFreq"].ToString();
                        list.Add(term, obj);
                    }
                    else
                    {
                        TermQuery obj = list[term];
                        obj.doc_id_term_pos = obj.doc_id_term_pos + "||" + sdrDoc["DocIdAndPos"].ToString();
                        obj.term_freq = obj.term_freq + "||" + sdrDoc["TermFreq"].ToString();
                        list[term] = obj;
                    }
                }
            }
            if (m)
            {
                Exact_Keyword_Search(list, Clean_Query, Query);
            }
            else
            {
                Multi_Keyword_Search(list, Clean_Query);
            }
        }
        public static void Exact_Keyword_Search(Dictionary<String, TermQuery> list, List<String> query,String Q)
        {
            foreach (var x in list.Values)
            {
                List<String> temp1 = new List<string>();
                List<int> c = new List<int>();
                String store = "";
                for (int i = 0; i < list[x.term].doc_id_term_pos.Length; i++)
                {
                    if (list[x.term].doc_id_term_pos[i] == '|' && list[x.term].doc_id_term_pos[i + 1] == '|')
                    {
                        temp1.Add(store);
                        store = "";
                        i += 2;
                    }
                    store += list[x.term].doc_id_term_pos[i];
                }
                temp1.Add(store);

                foreach (var t in temp1)
                {
                    String[] arr = t.Split(',').ToArray();
                    c.Add(int.Parse(arr[0]));
                }
                DocIntersection.Add(x.term, c);
            }
            List<int> disjoin = new List<int>();
            if (query.Count == 1)
            {
                disjoin = DocIntersection[query[0]];
            }
            else
            {
                List<int> res = DocIntersection[query[0]];
                foreach (var x in DocIntersection)
                {
                    if (!x.Key.Equals(query[0]))//skip
                    {
                        List<int> q = x.Value;
                        disjoin = res.Intersect(q).ToList();
                        res = disjoin;
                    }
                }
            }
            Dictionary<String, TermDetail> final = new Dictionary<String, TermDetail>();
            foreach (var x in list.Values)
            {
                List<String> temp1 = new List<string>();
                String store = "";
                for (int i = 0; i < list[x.term].doc_id_term_pos.Length; i++)
                {
                    if (list[x.term].doc_id_term_pos[i] == '|' && list[x.term].doc_id_term_pos[i + 1] == '|')
                    {
                        temp1.Add(store);
                        store = "";
                        i += 2;
                    }
                    store += list[x.term].doc_id_term_pos[i];
                }
                temp1.Add(store);
                String restemp = "";
                foreach (var t in temp1)
                {
                    String[] arr = t.Split(',').ToArray();
                    if (disjoin.Contains(int.Parse(arr[0])))
                    {
                        restemp += t + "|";
                    }
                }
                TermDetail obj;
                obj.doc_id_term_pos = restemp.Remove(restemp.Length - 1);
                obj.term_freq = x.term_freq;
                final.Add(x.term, obj);
            }
            Dictionary<int, int> FrqID = new Dictionary<int, int>();
            int min = 0, min1 = 0;
            int row = final.Count, j = 0;
            int col = final[query[0]].doc_id_term_pos.Split('|').Length;
            String[,] temp = new String[row, col];
            foreach (var b in final.Values)
            {
                String[] arr = b.doc_id_term_pos.Split('|').ToArray();
                for (int i = 0; i < col; i++)
                {
                    temp[j, i] = arr[i];
                }
                j++;
            }
            int frequ = 0;
            if (disjoin.Count != 0)
            {
                for (int i = 0; i < col; i++)
                {
                    bool flag = true;
                    int docId = int.Parse(temp[0, i].Split(',').ToArray()[0]);
                    min1 = 0;
                    frequ = 0;
                    for (j = 0; j < row - 1; j++)
                    {
                        min = int.MaxValue;
                        String[] first = temp[j, i].Split(',').ToArray()[1].Split(':').ToArray();
                        String[] second = temp[j + 1, i].Split(',').ToArray()[1].Split(':').ToArray();
                        flag = true;
                        for (int u = 0; u < first.Length; u++)
                        {
                            for (int m = 0; m < second.Length; m++)
                            {
                                if ((1== int.Parse(second[m]) - int.Parse(first[u])) && (int.Parse(first[u]) < int.Parse(second[m])))
                                {
                                    min = Math.Abs(int.Parse(first[u]) - int.Parse(second[m]));
                                    flag = false;
                                    frequ++;
                                }
                            }
                        }
                        if (flag) min1 += 5;
                        else min1 += min;
                    }
                    if(min1== query.Count - 1)
                    {
                        FrqID.Add(docId,frequ);
                    }
                   
                }
                foreach (var x in FrqID.OrderBy(key => key.Value))
                {
                    SqlConnection con = new SqlConnection(connString);
                    string quer = "select * from Web_pages_Details Where Id = @T";
                    con.Open();
                    SqlCommand c = new SqlCommand(quer, con);
                    c.Parameters.AddWithValue("@T", x.Key);
                    SqlDataReader sdrDoc = c.ExecuteReader();
                    while (sdrDoc.Read())
                    {
                        Console.WriteLine(sdrDoc["Uri"].ToString());
                    }
                    con.Close();
                }

            }
        }
        public static void Multi_Keyword_Search(Dictionary<String,TermQuery> list,List<String>query)
        {   
        foreach(var x in list.Values)
        {
                List<String> temp1 = new List<String>();
                List<int> c = new List<int>();
                String store = "";
                for (int i = 0; i < list[x.term].doc_id_term_pos.Length; i++)
                {
                    if (list[x.term].doc_id_term_pos[i] == '|' && list[x.term].doc_id_term_pos[i + 1] == '|')
                    {
                        temp1.Add(store);
                        store = "";
                        i += 2;
                    }
                    store += list[x.term].doc_id_term_pos[i];
                }
                temp1.Add(store);

                foreach (var t in temp1)
                {
                    String[] arr = t.Split(',').ToArray();
                    c.Add(int.Parse(arr[0]));
                }
                DocIntersection.Add(x.term, c);
            }
            IEnumerable<int> difference = new List<int>(); ;
            IEnumerable<int> res1 = DocIntersection[query[0]];
            foreach (var x in DocIntersection)
            {
                if (!x.Key.Equals(query[0]))
                {
                    IEnumerable<int> q = x.Value;
                    difference = (res1.Except(q).ToList().Concat(q.Except(res1).ToList()));
                    res1 = difference;
                }
            }
            List<int> disjoin = new List<int>();
            if (query.Count == 1)
            {
                disjoin = DocIntersection[query[0]];
            }
            else
            {
                List<int> res = DocIntersection[query[0]];
                foreach (var x in DocIntersection)
                {
                    if (!x.Key.Equals(query[0]))
                    {
                        List<int> q = x.Value;
                        disjoin = res.Intersect(q).ToList();
                        res = disjoin;
                    }
                }
            }
            Dictionary<String, TermDetail> final = new Dictionary<String, TermDetail>();
            foreach (var x in list.Values)
            {
                List<String> temp1 = new List<String>();
                String store = "";
                for (int i = 0; i < list[x.term].doc_id_term_pos.Length; i++)
                {
                    if (list[x.term].doc_id_term_pos[i] == '|' && list[x.term].doc_id_term_pos[i + 1] == '|')
                    {
                        temp1.Add(store);
                        store = "";
                        i += 2;
                    }
                    store += list[x.term].doc_id_term_pos[i];
                }
                temp1.Add(store);
                String restemp = "";
                foreach (var t in temp1)
                {
                        String[] arr = t.Split(',').ToArray();
                        if (disjoin.Contains(int.Parse(arr[0])))
                        {
                            restemp += t + "|";
                        }
                }
                TermDetail obj;
                obj.doc_id_term_pos = restemp.Remove(restemp.Length-1);
                obj.term_freq = x.term_freq;
                final.Add(x.term, obj);
            }
            Dictionary<int, int> docDis = new Dictionary<int, int>();
            int min = 0,min1=0;
          //  String row1;
            int row = final.Count,j=0;
            int col = final[query[0]].doc_id_term_pos.Split('|').Length;
            String[,] temp = new String[row, col];
            foreach (var b in final.Values)
            {
                String[] arr = b.doc_id_term_pos.Split('|').ToArray();
                for (int i = 0; i < col; i++)
                {
                        temp[j,i] = arr[i];
                }
                j++;
            }
            if (disjoin.Count != 0)
            {
                for (int i = 0; i < col; i++)
                {
                    bool flag = true;
                    int docId = int.Parse(temp[0, i].Split(',').ToArray()[0]);
                    min1 = 0;
                    for (j = 0; j < row - 1; j++)
                    {
                        min = int.MaxValue;
                        String[] first = temp[j, i].Split(',').ToArray()[1].Split(':').ToArray();
                        String[] second = temp[j + 1, i].Split(',').ToArray()[1].Split(':').ToArray();
                        flag = true;
                        for (int u = 0; u < first.Length; u++)
                        {
                            for (int m = 0; m < second.Length; m++)
                            {
                                if ((min > int.Parse(second[m]) - int.Parse(first[u])) && (int.Parse(first[u]) <int.Parse(second[m])))
                                {
                                    min = Math.Abs(int.Parse(first[u]) - int.Parse(second[m]));
                                    flag = false;
                                }
                            }
                        }
                        if (flag) min1 += 0;
                        else  min1 += min;
                    }
                    docDis.Add(docId, min1);
                }

                foreach (var e in docDis.OrderBy(key=>key.Value))
                {
                    SqlConnection con = new SqlConnection(connString);
                    string quer = "select * from Web_pages_Details Where Id = @T";
                    con.Open();
                    SqlCommand c = new SqlCommand(quer, con);
                    c.Parameters.AddWithValue("@T", e.Key);
                    SqlDataReader sdrDoc = c.ExecuteReader();
                    while (sdrDoc.Read())
                    {
                        Console.WriteLine(sdrDoc["Uri"].ToString());
                    }
                    con.Close();
                }
                foreach(var e in difference)
                {
                    SqlConnection con = new SqlConnection(connString);
                    string quer = "select * from Web_pages_Details Where Id = @T";
                    con.Open();
                    SqlCommand c = new SqlCommand(quer, con);
                    c.Parameters.AddWithValue("@T", e);
                    SqlDataReader sdrDoc = c.ExecuteReader();
                    while (sdrDoc.Read())
                    {
                        Console.WriteLine(sdrDoc["Uri"].ToString());
                    }
                    con.Close();
                }
            }
        }

        public static List<String> Linquistics_Query(List<String> terms)
        {
            List<String> Terms = new List<String>();
            foreach (var term in terms)
            {
                String temp = term;
                foreach (String punc in PUNCTUATION)
                {
                    if (temp.Contains(punc))
                    {
                        temp = temp.Replace(punc, "");
                    }
                }
                var real = new EnglishPorter2Stemmer();
                Terms.Add(real.Stem(temp.ToLower()).Value);
            }
            for (int i = 0; i < Terms.Count; i++)
            {
                if (StopWords.Contains(Terms.ElementAt(i)))
                {
                    Terms.RemoveAt(i);
                }
            }
            return Terms;
        }
        static void Inverted_Index()
        {
            SqlConnection con = new SqlConnection(connString);
            string query = "select * from Web_pages_Details where Id<=1600";
            con.Open();
            SqlCommand c = new SqlCommand(query, con);
            SqlDataReader sdrDoc = c.ExecuteReader();
            while (sdrDoc.Read())
            {
                List<String> Terms = TokanizationMethod(sdrDoc["Body"].ToString());
                Terms = Terms.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                List<String> FinalTerms = clean(Terms);
                LinguisticsMethod(FinalTerms, sdrDoc["Id"].ToString());
            }
            DBInsert(TermsDataBaseSql);
        }
        public static void DBInsert(Dictionary<String, TermDetail> res)
        {
            int c = 0;
            int counter = 1;
            SqlConnection con = new SqlConnection(connString);
            con.Open();
            foreach (var r in res)
            {
                Console.WriteLine("Term Id " + counter + " Done");
                counter++;
                if (c == res.Count) break;

                string query = "insert into Inverted_Index(Term,DocIdAndPos,TermFreq) values(@T,@P,@F)";
                SqlCommand command = new SqlCommand(query, con);
                command.Parameters.Add("@T", r.Key);
                command.Parameters.Add("@P", r.Value.doc_id_term_pos);
                command.Parameters.Add("@F", r.Value.term_freq);
                command.ExecuteNonQuery();
            }
            con.Close();
        }
        static List<String> clean(List<String> Terms)
        {
            List<String> FinalTerms = new List<String>();
            foreach (var x in Terms)
            {
                if (Regex.IsMatch(Regex.Replace(x, @"\?+", ""), "[a-zA-Z0-9]"))
                {
                    FinalTerms.Add(x.Trim());
                }
            }
            return FinalTerms;
        }
        static void LinguisticsMethod(List<String> terms, String Doc_Id)
        {
            List<String> Terms = new List<String>();
            foreach (var term in terms)
            {
                String temp = term;
                foreach (String punc in PUNCTUATION)
                {
                    if (temp.Contains(punc))
                    {
                        temp = temp.Replace(punc, "");
                    }
                }
                var real = new EnglishPorter2Stemmer();
                Terms.Add(real.Stem(temp.ToLower()).Value);
            }
            Dictionary<String, TermDetails> TD = new Dictionary<String, TermDetails>();
            for (int i = 0; i < Terms.Count; i++)
            {
                if (!StopWords.Contains(Terms.ElementAt(i)))
                {
                    if (!TD.ContainsKey(Terms.ElementAt(i)))
                    {
                        TermDetails obj;
                        obj.doc_id = Doc_Id; obj.term_pos = i + ""; obj.term_freq = 1;
                        TD.Add(Terms.ElementAt(i), obj);
                    }
                    else
                    {
                        TermDetails obj = TD[Terms.ElementAt(i)];
                        obj.term_pos = obj.term_pos + ":" + i; obj.term_freq = obj.term_freq + 1;
                        TD[Terms.ElementAt(i)] = obj;
                    }
                }
            }

            foreach (var item in TD)
            {
                flag = true;
                int count = 0;
                foreach (var term in TermsDataBaseSql)
                {
                    if (count == TermsDataBaseSql.Count) break;
                    count++;

                    if (term.Key.Equals(item.Key))
                    {
                        flag = false;
                        TermDetail obj = term.Value;
                        obj.doc_id_term_pos = obj.doc_id_term_pos + "||" + item.Value.doc_id + "," + item.Value.term_pos;
                        obj.term_freq = obj.term_freq + "||" + item.Value.term_freq;
                        TermsDataBaseSql[item.Key] = obj;
                        break;
                    }
                }
                if (flag == true)
                {
                    TermDetail obj;
                    obj.doc_id_term_pos = item.Value.doc_id + "," + item.Value.term_pos; obj.term_freq = item.Value.term_freq + "";
                    TermsDataBaseSql.Add(item.Key, obj);
                }
            }
        }
        static List<String> TokanizationMethod(String Doc)
        {
            List<String> Terms = new List<String>();
            String store = "";
            for (int i = 0; i < Doc.Length; i++)
            {
                if (Doc[i] == ' ' || Doc[i] == ',')
                {
                    Terms.Add(store);
                    store = "";
                }
                store += Doc[i];
            }
            Terms.Add(store);
            return Terms;
        }
        public static void Web_Crawrar()
        {
            URL = "http://www.bbc.com";
            myWebRequest = WebRequest.Create(URL);
            myWebResponse = myWebRequest.GetResponse();
            streamResponse = myWebResponse.GetResponseStream();
            sReader = new StreamReader(streamResponse);
            rString = sReader.ReadToEnd();
            streamResponse.Close();
            sReader.Close();
            myWebResponse.Close();

            var pageCon = new HtmlDocument();
            pageCon.LoadHtml(rString);
            var PageBody = pageCon.DocumentNode.InnerText;
            //    DB(URL, PageBody);

            Links_Not_Visited.Enqueue(URL);
            Links_Visited.Add(URL);


            while (Links_Visited.Count < 3001)
            {
                try
                {
                    URL = Links_Not_Visited.Dequeue();

                    myWebRequest = WebRequest.Create(URL);
                    myWebResponse = myWebRequest.GetResponse();
                    streamResponse = myWebResponse.GetResponseStream();
                    sReader = new StreamReader(streamResponse);
                    rString = sReader.ReadToEnd();
                    streamResponse.Close();
                    sReader.Close();
                    myWebResponse.Close();

                    IHTMLDocument2 myDoc = new HTMLDocumentClass();
                    myDoc.write(rString);
                    IHTMLElementCollection elements = myDoc.links;
                    foreach (IHTMLElement el in elements)
                    {
                        string link = (string)el.getAttribute("href", 0);
                        if (!Links_Visited.Contains(link) && RemoteFileExists(link))
                        {
                            try
                            {
                                myWebRequest = WebRequest.Create(link);
                                myWebResponse = myWebRequest.GetResponse();
                                streamResponse = myWebResponse.GetResponseStream();
                                sReader = new StreamReader(streamResponse);
                                rString = sReader.ReadToEnd();
                                streamResponse.Close();
                                sReader.Close();
                                myWebResponse.Close();

                                var pageDoc1 = new HtmlDocument();
                                pageDoc1.LoadHtml(rString);
                                var Body1 = pageDoc1.DocumentNode.InnerText;
                                if (GetLanguage(Body1).Equals("eng") && Regex.IsMatch(Body1.Replace("\n", "").Replace(" ", "").Substring(0, 10), @"^[a-zA-Z0-9]+$"))
                                {
                                    Console.WriteLine("The language of the text is '{0}' (ISO639-3 code)", "eng");
                                    Console.WriteLine("Number Of Link is : " + numLink++);
                                    //    DB(link, Body1);
                                    Links_Not_Visited.Enqueue(link);
                                    Links_Visited.Add(link);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
        public static void DB(string page_url, string page_content)
        {
            SqlConnection con = new SqlConnection(connString);
            string query = "insert into Web_pages_Details(Uri,Body) values(@PageURL,@PageContent)";
            con.Open();
            SqlCommand command = new SqlCommand(query, con);
            command.Parameters.Add("@PageURL", page_url);
            command.Parameters.Add("@PageContent", page_content);
            command.ExecuteNonQuery();
            con.Close();
        }
        public static bool RemoteFileExists(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AllowAutoRedirect = false;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                //Returns TRUE if the Status code == 200
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                return false;
            }
        }
        public static String GetLanguage(String body)
        {
            var factory = new RankedLanguageIdentifierFactory();
            var identifier = factory.Load(@"C:\Users\Lenovo\source\repos\Web_Crawler\Web_Crawler/Core14.profile.xml");
            var languages = identifier.Identify(body);
            var mostCertainLanguage = languages.FirstOrDefault();
            if (mostCertainLanguage != null)
            {
                if (mostCertainLanguage.Item1.Iso639_3.Equals("eng"))
                {
                    return mostCertainLanguage.Item1.Iso639_3;
                }
                else
                {
                    Console.WriteLine("The language of the text is '{0}' (ISO639-3 code)", mostCertainLanguage.Item1.Iso639_3);
                    return "not eng";
                }
            }
            else
            {
                Console.WriteLine("The language couldn’t be identified with an acceptable degree of certainty");
                return "not language";
            }
        }
    }
}
