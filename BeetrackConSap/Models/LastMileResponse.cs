namespace BeetrackConSap.Models {
    public class LastMileResponse {
        public string status { get; set; }
        public ResponseData response { get; set; }
    }

    public class ResponseData {
        public int route_id { get; set; }
    }
}
