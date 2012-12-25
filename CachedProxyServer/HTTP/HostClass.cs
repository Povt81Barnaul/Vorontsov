using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CachedProxyServer.HTTP
{
    public class HostClass : BaseClass
    {
        private string host = String.Empty;
        private int port = 80;

        /// <summary>
        /// Адрес хоста (домен)
        /// </summary>
        public string Host
        {
            get
            {
                return host;
            }
        }

        /// <summary>
        /// Номер порта, по умолчанию - 80
        /// </summary>
        public int Port
        {
            get
            {
                return port;
            }
        }

        public HostClass(string source) : base(source)
        {
            // пасим данные
            Regex myReg = new Regex(@"^(((?<host>.+?):(?<port>\d+?))|(?<host>.+?))$");
            Match m = myReg.Match(source);
            host = m.Groups["host"].Value;
            if (!int.TryParse(m.Groups["port"].Value, out port))
            { // не удалось преобразовать порт в число, значит порт не указан, ставим значение по умолчанию
               port = 80;
            }
        }
    }
}
