using System.Globalization;

namespace ChessMAUI.Services;

public static class GeoData
{
    public static readonly List<string> Countries = new()
    {
        "Afeganistão","África do Sul","Albânia","Alemanha","Andorra","Angola",
        "Antígua e Barbuda","Arábia Saudita","Argélia","Argentina","Armênia",
        "Austrália","Áustria","Azerbaijão","Bahamas","Bangladeche","Barbados",
        "Barein","Bélgica","Belize","Benim","Bielo-Rússia","Bolívia","Bósnia e Herzegovina",
        "Botsuana","Brasil","Brunei","Bulgária","Burquina Fasso","Burundi","Butão",
        "Cabo Verde","Camarões","Camboja","Canadá","Catar","Cazaquistão","Chade",
        "Chile","China","Chipre","Colômbia","Comores","Congo","Coreia do Norte",
        "Coreia do Sul","Costa do Marfim","Costa Rica","Croácia","Cuba","Dinamarca",
        "Djibuti","Dominica","Egito","El Salvador","Emirados Árabes Unidos","Equador",
        "Eritreia","Eslováquia","Eslovênia","Espanha","Estados Unidos","Estônia",
        "Essuatíni","Etiópia","Fiji","Filipinas","Finlândia","França","Gabão",
        "Gâmbia","Gana","Geórgia","Granada","Grécia","Guatemala","Guiana","Guiné",
        "Guiné Equatorial","Guiné-Bissau","Haiti","Honduras","Hungria","Iêmen",
        "Ilhas Marshall","Ilhas Salomão","Índia","Indonésia","Irã","Iraque","Irlanda",
        "Islândia","Israel","Itália","Jamaica","Japão","Jordânia","Kiribati","Kosovo",
        "Kuwait","Laos","Lesoto","Letônia","Líbano","Libéria","Líbia","Liechtenstein",
        "Lituânia","Luxemburgo","Madagascar","Malásia","Maláui","Maldivas","Mali",
        "Malta","Marrocos","Mauritânia","Maurício","México","Micronésia","Moçambique",
        "Moldávia","Mônaco","Mongólia","Montenegro","Mianmar","Namíbia","Nauru",
        "Nepal","Nicarágua","Níger","Nigéria","Noruega","Nova Zelândia","Omã",
        "Países Baixos","Palau","Palestina","Panamá","Papua Nova Guiné","Paquistão",
        "Paraguai","Peru","Polônia","Portugal","Quênia","Quirguistão","Reino Unido",
        "República Centro-Africana","República Dominicana","República Tcheca",
        "Romênia","Ruanda","Rússia","Samoa","San Marino","Santa Lúcia",
        "São Cristóvão e Névis","São Tomé e Príncipe","São Vicente e Granadinas",
        "Senegal","Serra Leoa","Sérvia","Seychelles","Singapura","Síria","Somália",
        "Sri Lanka","Sudão","Sudão do Sul","Suécia","Suíça","Suriname","Tailândia",
        "Tanzânia","Timor-Leste","Togo","Tonga","Trindade e Tobago","Tunísia",
        "Turcomenistão","Turquia","Tuvalu","Ucrânia","Uganda","Uruguai","Uzbequistão",
        "Vanuatu","Vaticano","Venezuela","Vietnã","Zâmbia","Zimbábue"
    };

    public static readonly List<string> BrazilStates = new()
    {
        "AC – Acre","AL – Alagoas","AP – Amapá","AM – Amazonas","BA – Bahia",
        "CE – Ceará","DF – Distrito Federal","ES – Espírito Santo","GO – Goiás",
        "MA – Maranhão","MT – Mato Grosso","MS – Mato Grosso do Sul",
        "MG – Minas Gerais","PA – Pará","PB – Paraíba","PR – Paraná",
        "PE – Pernambuco","PI – Piauí","RJ – Rio de Janeiro",
        "RN – Rio Grande do Norte","RS – Rio Grande do Sul","RO – Rondônia",
        "RR – Roraima","SC – Santa Catarina","SP – São Paulo",
        "SE – Sergipe","TO – Tocantins"
    };

    // Retorna só a sigla (ex: "SP") a partir de "SP – São Paulo"
    public static string StateAbbr(string? pickerValue)
        => pickerValue?.Split('–')[0].Trim() ?? "";

    // Nome do país (em Countries) → código ISO 3166-1 alpha-2 (minúsculo, usado na URL da bandeira)
    public static readonly Dictionary<string, string> CountryCodes = new()
    {
        ["Afeganistão"] = "af", ["África do Sul"] = "za", ["Albânia"] = "al",
        ["Alemanha"] = "de", ["Andorra"] = "ad", ["Angola"] = "ao",
        ["Antígua e Barbuda"] = "ag", ["Arábia Saudita"] = "sa", ["Argélia"] = "dz",
        ["Argentina"] = "ar", ["Armênia"] = "am", ["Austrália"] = "au",
        ["Áustria"] = "at", ["Azerbaijão"] = "az", ["Bahamas"] = "bs",
        ["Bangladeche"] = "bd", ["Barbados"] = "bb", ["Barein"] = "bh",
        ["Bélgica"] = "be", ["Belize"] = "bz", ["Benim"] = "bj",
        ["Bielo-Rússia"] = "by", ["Bolívia"] = "bo", ["Bósnia e Herzegovina"] = "ba",
        ["Botsuana"] = "bw", ["Brasil"] = "br", ["Brunei"] = "bn",
        ["Bulgária"] = "bg", ["Burquina Fasso"] = "bf", ["Burundi"] = "bi",
        ["Butão"] = "bt", ["Cabo Verde"] = "cv", ["Camarões"] = "cm",
        ["Camboja"] = "kh", ["Canadá"] = "ca", ["Catar"] = "qa",
        ["Cazaquistão"] = "kz", ["Chade"] = "td", ["Chile"] = "cl",
        ["China"] = "cn", ["Chipre"] = "cy", ["Colômbia"] = "co",
        ["Comores"] = "km", ["Congo"] = "cg", ["Coreia do Norte"] = "kp",
        ["Coreia do Sul"] = "kr", ["Costa do Marfim"] = "ci", ["Costa Rica"] = "cr",
        ["Croácia"] = "hr", ["Cuba"] = "cu", ["Dinamarca"] = "dk",
        ["Djibuti"] = "dj", ["Dominica"] = "dm", ["Egito"] = "eg",
        ["El Salvador"] = "sv", ["Emirados Árabes Unidos"] = "ae", ["Equador"] = "ec",
        ["Eritreia"] = "er", ["Eslováquia"] = "sk", ["Eslovênia"] = "si",
        ["Espanha"] = "es", ["Estados Unidos"] = "us", ["Estônia"] = "ee",
        ["Essuatíni"] = "sz", ["Etiópia"] = "et", ["Fiji"] = "fj",
        ["Filipinas"] = "ph", ["Finlândia"] = "fi", ["França"] = "fr",
        ["Gabão"] = "ga", ["Gâmbia"] = "gm", ["Gana"] = "gh",
        ["Geórgia"] = "ge", ["Granada"] = "gd", ["Grécia"] = "gr",
        ["Guatemala"] = "gt", ["Guiana"] = "gy", ["Guiné"] = "gn",
        ["Guiné Equatorial"] = "gq", ["Guiné-Bissau"] = "gw", ["Haiti"] = "ht",
        ["Honduras"] = "hn", ["Hungria"] = "hu", ["Iêmen"] = "ye",
        ["Ilhas Marshall"] = "mh", ["Ilhas Salomão"] = "sb", ["Índia"] = "in",
        ["Indonésia"] = "id", ["Irã"] = "ir", ["Iraque"] = "iq",
        ["Irlanda"] = "ie", ["Islândia"] = "is", ["Israel"] = "il",
        ["Itália"] = "it", ["Jamaica"] = "jm", ["Japão"] = "jp",
        ["Jordânia"] = "jo", ["Kiribati"] = "ki", ["Kosovo"] = "xk",
        ["Kuwait"] = "kw", ["Laos"] = "la", ["Lesoto"] = "ls",
        ["Letônia"] = "lv", ["Líbano"] = "lb", ["Libéria"] = "lr",
        ["Líbia"] = "ly", ["Liechtenstein"] = "li", ["Lituânia"] = "lt",
        ["Luxemburgo"] = "lu", ["Madagascar"] = "mg", ["Malásia"] = "my",
        ["Maláui"] = "mw", ["Maldivas"] = "mv", ["Mali"] = "ml",
        ["Malta"] = "mt", ["Marrocos"] = "ma", ["Mauritânia"] = "mr",
        ["Maurício"] = "mu", ["México"] = "mx", ["Micronésia"] = "fm",
        ["Moçambique"] = "mz", ["Moldávia"] = "md", ["Mônaco"] = "mc",
        ["Mongólia"] = "mn", ["Montenegro"] = "me", ["Mianmar"] = "mm",
        ["Namíbia"] = "na", ["Nauru"] = "nr", ["Nepal"] = "np",
        ["Nicarágua"] = "ni", ["Níger"] = "ne", ["Nigéria"] = "ng",
        ["Noruega"] = "no", ["Nova Zelândia"] = "nz", ["Omã"] = "om",
        ["Países Baixos"] = "nl", ["Palau"] = "pw", ["Palestina"] = "ps",
        ["Panamá"] = "pa", ["Papua Nova Guiné"] = "pg", ["Paquistão"] = "pk",
        ["Paraguai"] = "py", ["Peru"] = "pe", ["Polônia"] = "pl",
        ["Portugal"] = "pt", ["Quênia"] = "ke", ["Quirguistão"] = "kg",
        ["Reino Unido"] = "gb", ["República Centro-Africana"] = "cf",
        ["República Dominicana"] = "do", ["República Tcheca"] = "cz",
        ["Romênia"] = "ro", ["Ruanda"] = "rw", ["Rússia"] = "ru",
        ["Samoa"] = "ws", ["San Marino"] = "sm", ["Santa Lúcia"] = "lc",
        ["São Cristóvão e Névis"] = "kn", ["São Tomé e Príncipe"] = "st",
        ["São Vicente e Granadinas"] = "vc", ["Senegal"] = "sn", ["Serra Leoa"] = "sl",
        ["Sérvia"] = "rs", ["Seychelles"] = "sc", ["Singapura"] = "sg",
        ["Síria"] = "sy", ["Somália"] = "so", ["Sri Lanka"] = "lk",
        ["Sudão"] = "sd", ["Sudão do Sul"] = "ss", ["Suécia"] = "se",
        ["Suíça"] = "ch", ["Suriname"] = "sr", ["Tailândia"] = "th",
        ["Tanzânia"] = "tz", ["Timor-Leste"] = "tl", ["Togo"] = "tg",
        ["Tonga"] = "to", ["Trindade e Tobago"] = "tt", ["Tunísia"] = "tn",
        ["Turcomenistão"] = "tm", ["Turquia"] = "tr", ["Tuvalu"] = "tv",
        ["Ucrânia"] = "ua", ["Uganda"] = "ug", ["Uruguai"] = "uy",
        ["Uzbequistão"] = "uz", ["Vanuatu"] = "vu", ["Vaticano"] = "va",
        ["Venezuela"] = "ve", ["Vietnã"] = "vn", ["Zâmbia"] = "zm",
        ["Zimbábue"] = "zw",
    };

    // URL da imagem da bandeira (flagcdn.com — CDN público, gratuito, sem chave de API).
    // Usar imagem de verdade em vez de emoji porque Windows não renderiza emoji de bandeira
    // (mostra só o código "BR" em texto), enquanto a imagem funciona igual em toda plataforma.
    public static string? FlagUrl(string? countryName, int width = 40)
    {
        if (string.IsNullOrEmpty(countryName)) return null;
        return CountryCodes.TryGetValue(countryName, out var code)
            ? $"https://flagcdn.com/w{width}/{code}.png"
            : null;
    }

    // Sugere um país com base na região configurada no aparelho (sem pedir permissão de
    // localização — só lê a configuração de idioma/região do sistema). O usuário sempre
    // pode corrigir manualmente no Picker; isso é só um valor inicial pra evitar perfil vazio.
    public static string? SuggestCountryFromDevice()
    {
        try
        {
            string iso = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToLowerInvariant();
            foreach (var kv in CountryCodes)
                if (kv.Value == iso) return kv.Key;
        }
        catch { /* região indisponível na plataforma — sem sugestão */ }
        return null;
    }
}
