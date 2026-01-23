using SCRM.API.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCRM.SHARED.Models
{
    public class Wx
    {
        public string? wxid => wechatAccount?.wxid;
        public bool isOnline => String.IsNullOrEmpty(wechatAccount?.wxid) ? false : true;
        public SrClient? srClient { get; set; } = null;
        public WechatAccount? wechatAccount { get; set; } = null;
        public List<Contact>? contacts { get; set; } = null;

    }
}
