using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Web_Crawler
{
    public class Kgram
    {
        public List<String> AfterKGram = new List<string>();
        public  List<String> StopWords = new List<String>()
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
        public String connString = "Data Source=FADYCSASU\\SQLEXPRESS;Initial Catalog=Search_Engine;Integrated Security=True";
        public void createTableTermKgram()
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
                foreach (var word in FinalTerms)
                {
                    if (!StopWords.Contains(word))
                    {
                        String store = K_Gram_Method(2, word);
                        DB(word, store);
                    }
                }
            }
        }
        public  void DB(string word, string gram)
        {
            SqlConnection con = new SqlConnection(connString);
            string query = "insert into K_Gram(Term,Gram) values(@T,@G)";
            con.Open();
            SqlCommand command = new SqlCommand(query, con);
            command.Parameters.Add("@T", word);
            command.Parameters.Add("@G", gram);
            command.ExecuteNonQuery();
            con.Close();
        }
        public List<String> clean(List<String> Terms)
        {
            List<String> FinalTerms = new List<String>();
            foreach (var x in Terms)
            {
                if (Regex.IsMatch(Regex.Replace(x, @"\?+", ""), "[a-zA-Z0-9]"))
                {
                    FinalTerms.Add(x.ToLower().Trim());
                }
            }
            return FinalTerms;
        }
        public List<String> TokanizationMethod(String Doc)
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
        public string K_Gram_Method(int k, String term)
        {
            String res ="";
            res="$" + term[0]+" ";
            for(int i = 0; i < (term.Length - (k - 1)); i++)
            {
                String temp = "";
                for(int j = i; j < i + k; j++)
                {
                    temp += term[j];
                }
                res+=temp+" ";
            }
            res+="$" + term[term.Length - 1];
            return res;
        }
        public float JaccordCoefficient(String high, String s)
        {
            List<String> l1 = high.Split(' ').ToList();
            List<String> l2 = s.Split(' ').ToList();
            List<String> intersect =l1.Intersect(l2).ToList();
            float x = (float)((float)(intersect.Count) /(float) (l1.Count * 2 - intersect.Count));
            return ((float)(intersect.Count) /(l1.Count * 2- intersect.Count));
        }
        public int edit_Distance(String term,String query,int t,int q)
        {
            if (t == 0) return q;
            if (q == 0) return t;
            if (term[t - 1] == query[q - 1])
            {
                return edit_Distance(term, query, t - 1, q - 1);
            }
            return 1 + Math.Min(edit_Distance(term, query, t, q - 1), //insert
              Math.Min(edit_Distance(term,query,t-1,q),edit_Distance(term,query,t-1,q-1)));//remove replace
        }
        public bool Search_Query(String query)
        {
            Dictionary<String, String> DBKGram = new Dictionary<string, string>();
            SqlConnection con = new SqlConnection(connString);
            string q = "select * from K_Gram";
            con.Open();
            SqlCommand c = new SqlCommand(q, con);
            SqlDataReader sdrDoc = c.ExecuteReader();
            while (sdrDoc.Read())
            {
                if (!DBKGram.ContainsKey(sdrDoc["Term"].ToString()))
                    DBKGram.Add(sdrDoc["Term"].ToString(), sdrDoc["Gram"].ToString());
            }

            // DBKGram.Add("bbc", K_Gram_Method(2,"bbc"));
            //DBKGram.Add("computer", K_Gram_Method(2, "computer"));
            con.Close();
            List<String> Terms = TokanizationMethod(query);
            Terms = Terms.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            List<String> beforeKGram = clean(Terms);
            
            
            List<List<String>> grams = new List<List<string>>();
       
            foreach (var word in beforeKGram)
            {
                String[] s = K_Gram_Method(2, word).Split(' ').ToArray();
                String ss = K_Gram_Method(2, word);
                foreach (var x in s)
                {
                    List<String> temp = new List<string>();
                    foreach (var z in DBKGram)
                   {
                        if (z.Value.Contains(x))
                        {
                            temp.Add(z.Key);
                        }
                   }
                    grams.Add(temp);
                }
                HashSet<String> r = new HashSet<string>();                
                for (int i = 0; i < grams.Count; i++)
                {
                    foreach(var j in grams[i])
                    {
                        String high = DBKGram[j];
                        float jaccard = JaccordCoefficient(high, ss);
                        if (jaccard > 0.3)
                        {
                            r.Add(j);
                        }
                    }
                }
                String St = "";
                int max = int.MaxValue;
                foreach(var w in r)
                {
                    int dis = edit_Distance(w, word, w.Length, word.Length);
                  //   Console.WriteLine(w);

                    if (max > dis)
                    {
                        max = dis;
                        St = w;
                    }
                }
                AfterKGram.Add(St);
            }
            bool flag = true;
            for(int i = 0; i < AfterKGram.Count; i++)
            {
                if (!AfterKGram[i].Equals(beforeKGram[i]))
                {
                    flag = false;
                }
            }
            return flag;
        }    
    }
}
