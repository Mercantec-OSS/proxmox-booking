public static class Config
{
    public static string DB_CONNECTION_STRING => ParseVariable("DB_CONNECTION_STRING");
    public static string DB_CONNECTION_STRING_WITH_IP => GetDbConnectionStrignWithIp();
    public static string JWT_SECRET => ParseVariable("JWT_SECRET");
    public static string DOMAIN_ADDRESS => ParseVariable("DOMAIN_ADDRESS");

    public static string SMTP_ADDRESS => ParseSMTPConnectionString()[0];
    public static string SMTP_PORT => ParseSMTPConnectionString()[1];
    public static string SMTP_USER => ParseSMTPConnectionString()[2];
    public static string SMTP_PASSWORD => ParseSMTPConnectionString()[3];
    public static string EMAIL_TEMPLATES_PATH => ParseVariable("EMAIL_TEMPLATES_PATH");

    public static string PROXMOX_ADDR => ParseVariable("PROXMOX_ADDR");
    public static string PROXMOX_TOKEN_ID => ParseVariable("PROXMOX_TOKEN_ID");
    public static string PROXMOX_TOKEN_SECRET => ParseVariable("PROXMOX_TOKEN_SECRET");
    public static string PROXMOX_ISO_STORAGE => ParseVariable("PROXMOX_ISO_STORAGE");
    public static string PROXMOX_DATA_STORAGE => ParseVariable("PROXMOX_DATA_STORAGE");
    public static string PROXMOX_ADDR_IP => GetProxmoxAddressIp();

    private static string ParseVariable(string variableName) {
        string variable = Environment.GetEnvironmentVariable(variableName) ?? "";
        if (string.IsNullOrEmpty(variable)) {
            string errorMsg = $"{variableName} is not set";
            throw new Exception(errorMsg);
        }
        return variable;
    }

    private static string[] ParseSMTPConnectionString() {
        string smtpConnectionString = ParseVariable("SMTP_CONNECTION_STRING");
        string[] stringArgs = smtpConnectionString.Split("__");

        if (stringArgs.Length != 4) {
            string errorMsg = $"SMTP_CONNECTION_STRING is not in the correct format: '{smtpConnectionString}'";
            throw new Exception(errorMsg);
        }

        return stringArgs;
    }

    private static string GetProxmoxAddressIp() {
        string proxmoxAddress = PROXMOX_ADDR.Split(':').First();
        if (!IsIpv4(proxmoxAddress)) {
            throw new Exception("Proxmox address is not an ip address");
        }

        return proxmoxAddress;
    }

    // Special case for the connection string (Replace domain with ip for property work with the ServerVersion.Parse(connString))

    private static string GetAddressFromConnectionString(string connectionString)
    {
        var args = connectionString.Split(";");
        string address = "";
        foreach (var arg in args)
        {
            if (arg.ToLower().Contains("server"))
            {
                address = arg.Split("=").Last();
            }
        }

        // db address is not found
        if (string.IsNullOrEmpty(address))
        {
            throw new Exception("Server address not found in connection string");
        }

        return address;
    }
    private static bool IsIpv4(string address)
    {
        // check for 4 number grups 0.0.0.0 - 999.999.999.999
        string pattern = @"^(\d{1,3}\.){3}\d{1,3}$";
        Regex regex = new Regex(pattern);
        return regex.IsMatch(address);
    }

    private static string GetIpByDomain(string domainAddress)
    {
        // get ip by domain
        string ipAddress = "";
        IPAddress[] addresses = Dns.GetHostAddresses(domainAddress);
        foreach (IPAddress address in addresses)
        {
            string addressString = address.ToString();
            // check for ipv4 address
            if (IsIpv4(addressString)){
                ipAddress = addressString;
                break;
            }
        }

        // ip is not found
        if (string.IsNullOrEmpty(ipAddress))
        {
            throw new Exception("Ip address not found for the server address");
        }

        return ipAddress;
    }

    private static string GetDbConnectionStrignWithIp()
    {
        // get db address
        string domainAddress = GetAddressFromConnectionString(DB_CONNECTION_STRING);
        if (IsIpv4(domainAddress))
        {
            return DB_CONNECTION_STRING;
        }

        return DB_CONNECTION_STRING.Replace(domainAddress, GetIpByDomain(domainAddress));
    }
}