using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CachedProxyServer.HTTP
{
    public class BaseClass
    {
        private string _Source = String.Empty;

        public string Source
        {
            get
            {
                return _Source;
            }
        }

        public BaseClass(string source)
        {
            _Source = source;
        }
    }
}
