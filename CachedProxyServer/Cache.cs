using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace CachedProxyServer
{
    public class Cache
    {
        public HTTP.HTMLParser myHTTP;
        public HTTP.HTMLParser myReroutingHTTP;
        public DateTime LiveTime;

        public Cache(HTTP.HTMLParser myHTTP, HTTP.HTMLParser myReroutingHTTP, DateTime LiveTime)
        {
            this.myHTTP = myHTTP;
            this.myReroutingHTTP = myReroutingHTTP;
            this.LiveTime = LiveTime;
        }
    }
}
