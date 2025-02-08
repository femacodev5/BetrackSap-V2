namespace MorosidadWeb.Models {
    public interface IDecimalJsonConverter {
        bool CanConvert(Type typeToConvert);
    }
}