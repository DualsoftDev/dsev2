namespace Plc2DsApp
{
    public class AppRegistry
    {
        string _lastRead;
        string _lastWrite;
        Vendor _vendor;
        public AppRegistry()
        {
            _vendor = Vendor.LS;
        }
        public string LastRead
        {
            get => _lastRead;
            set
            {
                _lastRead = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
        public string LastWrite
        {
            get => _lastWrite;
            set
            {
                _lastWrite = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
        public Vendor Vendor
        {
            get => _vendor;
            set
            {
                _vendor = value;
                File.WriteAllText("lastFile.json", EmJson.ToJson(this));
            }
        }
    }
}
