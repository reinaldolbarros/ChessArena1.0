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
}
