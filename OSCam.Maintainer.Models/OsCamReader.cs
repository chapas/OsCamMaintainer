using OSCam.Maintainer;
using System.Text;

namespace OSCam.Maintainer.Models
{
    public class OsCamReader //http://www.faalsoft.com/knowledgebase/448/oscamserver.html
    {
        public const string reader = @"[reader]";
        public string label { get; set; } = ""; // same as label
        public string description { get; set; } // error;off;unknown;username
        public string enable { get; set; } = "1";
        public string protocol { get; set; } = "cccam"; //cccam ou newcam
        public string key { get; set; } = "";
        public string device { get; set; } = ""; //IP ou URL
        public string port { get; set; } = ""; //device port => "device,port"
        public string user { get; set; } = "";
        public string password { get; set; } = "";
        public string group { get; set; } = "1";
        public string inactivitytimeout { get; set; } = "30";
        public string reconnecttimeout { get; set; } = "30";
        public string lb_weight { get; set; } = "100";
        public string cccversion { get; set; } = "2.1.2";
        public string cccmaxhops { get; set; } = "10";
        public string cccwantemu { get; set; } = "1";
        public string ccckeepalive { get; set; } = "1";

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(reader);
            sb.AppendLine("label                    = " + label);
            if (!string.IsNullOrEmpty(description))
                sb.AppendLine("description              = " + description);
            sb.AppendLine("enable                   = " + enable);
            sb.AppendLine("protocol                 = " + protocol);
            if (!string.IsNullOrEmpty(key))
                sb.AppendLine("key= " + key);
            sb.AppendLine("device                   = " + device + "," + port);
            sb.AppendLine("user                     = " + user);
            sb.AppendLine("password                 = " + password);
            sb.AppendLine("group                    = " + group);
            sb.AppendLine("inactivitytimeout        = " + inactivitytimeout);
            sb.AppendLine("reconnecttimeout         = " + reconnecttimeout);
            sb.AppendLine("lb_weight                = " + lb_weight); 
            sb.AppendLine("cccversion               = " + cccversion);
            sb.AppendLine("cccmaxhops               = " + cccmaxhops);
            if (!string.IsNullOrEmpty(cccwantemu))
                sb.AppendLine("cccwantemu               = " + cccwantemu);
            sb.AppendLine("ccckeepalive             = " + ccckeepalive);
            sb.AppendLine();

            return sb.ToString();
        }

        public void UpdateNewFoundStateOnDescription(string newFoundState)
        {
            var readerDescriptionModel = new OsCamReaderDescription(description);
            readerDescriptionModel.UpdateWithNewFoundDescription(newFoundState);
            description = readerDescriptionModel.ToString();
        }
    }
}