using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CachedProxyServer.HTTP
{
    public class CollectionClass : Dictionary<string, BaseClass>
    {
        public CollectionClass() : base(StringComparer.CurrentCultureIgnoreCase) { }

        public void AddItem(string key, string source)
        {
            switch (key.Trim().ToLower())
            {
                case "host":
                // добавляем хост
                this.Add(key, new HostClass(source));
                break;

                case "content-type":
                // тип содержимого
                this.Add(key, new ContentTypeClass(source));
                break;

                default:
                // значения других ключей добавляем в виде строки
                this.Add(key, new BaseClass(source));
                break;
            }
        }

        public override string ToString()
        {
            string result = "";
            foreach (string k in this.Keys)
            {
                BaseClass itm = this[k];
                if (!String.IsNullOrEmpty(result))
                    result += "\r\n";
                result += String.Format("{0}: {1}", k, itm.Source);
            }
            return result;
        }
    }
}
