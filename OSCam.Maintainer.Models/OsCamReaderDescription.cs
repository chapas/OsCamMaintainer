using System;
using System.Text;

namespace OSCam.Maintainer
{
    public class OsCamReaderDescription
    {
        int Error { get; set; } = 0;
        int Off { get; set; } = 0;
        int Unknown { get; set; } = 0;
        public string Username { get; set; } = "";

        public OsCamReaderDescription(string description)
        {
            var asdf = description.Split(';', StringSplitOptions.None);

            if (asdf.Length != 4) 
                return; //dont's care whats on description field, gets 0;0;0

                Error = int.Parse(asdf[0]);
                Off = int.Parse(asdf[1]);
                Unknown = int.Parse(asdf[2]);
                Username =  string.IsNullOrEmpty(asdf[3]) ? "" : asdf[3];
        }

        public void UpdateWithNewFoundDescription(string newFoundState)
        {
            switch (newFoundState.ToLower())
            {
                case "off":
                    this.Off += 1;
                    break;
                case "unknown":
                    this.Unknown += 1;
                    break;
                case "error":
                    this.Error += 1;
                    break;
                default:
                    //connected to server, so reset fail couters
                    this.Off = 0;
                    this.Unknown = 0;
                    this.Error = 0;
                    break;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(Error.ToString());
            sb.Append(";");
            sb.Append(Off.ToString());
            sb.Append(";");
            sb.Append(Unknown.ToString());
            sb.Append(";");
            sb.Append(Username.ToString());

            return sb.ToString();
        }
    }
}
