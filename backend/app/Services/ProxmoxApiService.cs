public class ProxmoxApiService
{
    private HttpClient _client;

    public ProxmoxApiService()
    {
        _client = InitHttpClient();
    }

    public async Task<List<T>> GetProxmoxResources<T>(string type)
    {
        var response = await _client.GetAsync($"https://{Config.PROXMOX_ADDR}/api2/json/cluster/resources?type={type}");

        // get data from json response
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, List<T>>>();
        if (data == null)
        {
            return new List<T>();
        }

        return data["data"];
    }

    // tasks
    public async Task<List<ProxmoxTaskDto>> GetProxmoxTasks()
    {
        var response = await _client.GetAsync($"https://{Config.PROXMOX_ADDR}/api2/json/cluster/tasks");
        Dictionary<string, List<ProxmoxTaskDto>> data;
        var respData = await response.Content.ReadFromJsonAsync<Dictionary<string, List<ProxmoxTaskDto>>>();

        if (respData == null)
        {
            return new List<ProxmoxTaskDto>();
        }

        data = respData;
        return data["data"];
    }

    public async Task<ProxmoxTaskDto?> GetProxmoxTaskByUPID(string upid)
    {
        List<ProxmoxTaskDto> tasks = await GetProxmoxTasks();
        return tasks.FirstOrDefault(x => x.Upid == upid);
    }

    // templates
    public async Task<List<ProxmoxVmDto>> GetProxmoxTemplates()
    {
        List<ProxmoxVmDto> vmsAndTemplates = await GetProxmoxResources<ProxmoxVmDto>("vm");
        return vmsAndTemplates.Where(x => x.Template == 1).ToList();
    }

    public async Task<ProxmoxVmDto?> GetTemplateByNameAsync(string name)
    {
        List<ProxmoxVmDto> templates = await GetProxmoxTemplates();
        return templates.FirstOrDefault(x => x.Name == name);
    }

    // ISOs
    public async Task<List<ProxmoxIsoDto>> GetProxmoxIsoList(ProxmoxNodeDto node)
    {
        var response = await _client.GetAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{node.Node}/storage/{Config.PROXMOX_ISO_STORAGE}/content");
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, List<ProxmoxIsoDto>>>();

        // get data from json response
        if (data == null)
        {
            return new List<ProxmoxIsoDto>();
        }

        return data["data"].Where(x => x.Content.ToLower() == "iso").ToList();
    }

    public async Task MountIsoFile(ProxmoxVmDto vm, string filePath, bool waitTask = false)
    {
        var requestData = new Dictionary<string, object>
        {
            { "cdrom", filePath }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
        var response = await _client.PutAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/config", content);
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            await WaitForTaskFinish(upid);
        }
    }

    public async Task AttachIso(ProxmoxVmDto vm, ProxmoxIsoDto iso, bool waitTask = false)
    {
        await MountIsoFile(vm, iso.VolId, waitTask);
    }

    public async Task DetachIso(ProxmoxVmDto vm, bool waitTask = false)
    {
        await MountIsoFile(vm, "none", waitTask);
    }

    // VMs
    public async Task<List<ProxmoxVmDto>> GetProxmoxVms()
    {
        List<ProxmoxVmDto> vmsAndTemplates = await GetProxmoxResources<ProxmoxVmDto>("vm");
        return vmsAndTemplates.Where(x => x.Template == 0).ToList();
    }

    public async Task<ProxmoxVmDto?> GetVmByNameAsync(string name)
    {
        List<ProxmoxVmDto> vms = await GetProxmoxVms();
        return vms.FirstOrDefault(x => x.Name == name);
    }

    public async Task<ProxmoxVmDto?> GetVmByIdAsync(int id)
    {
        List<ProxmoxVmDto> vms = await GetProxmoxVms();
        return vms.FirstOrDefault(x => x.VmId == id);
    }

    public async Task<ProxmoxVmConfigDto> GetVmConfig(ProxmoxVmDto vm)
    {
        var response = await _client.GetAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/config");
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, ProxmoxVmConfigDto>>();
        if (data == null)
        {
            return new();
        }

        return data["data"];
    }

    public async Task<bool> CloneVm(int vmId, string vmName, ProxmoxVmDto template, ProxmoxNodeDto node, bool waitTask = false)
    {
        var data = new
        {
            newid = vmId,
            target = node.Node,
            full = 1,
            name = vmName
        };

        var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{template.Node}/qemu/{template.VmId}/clone", content);
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            return await WaitForTaskFinish(upid);
        }

        return true;
    }

    public async Task<bool> StopVm(ProxmoxVmDto vm, bool waitTask = false)
    {
        var response = await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/status/stop", new StringContent(""));
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            return await WaitForTaskFinish(upid);
        }

        return true;
    }

    public async Task<bool> StartVm(ProxmoxVmDto vm, bool waitTask = false)
    {
        var response = await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/status/start", new StringContent(""));
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            return await WaitForTaskFinish(upid);
        }

        return true;
    }

    public async Task<bool> DeleteVm(ProxmoxVmDto vm, bool waitTask = false)
    {
        var response = await _client.DeleteAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}?destroy-unreferenced-disks=1&purge=1");
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            return await WaitForTaskFinish(upid);
        }

        return true;
    }

    public async Task<ProxmoxVncDto> GetVncInfo(ProxmoxVmDto vm)
    {
        var requestData = new Dictionary<string, object>
        {
            { "generate-password", 1 }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/vncproxy", content);
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, ProxmoxVncDto>>();
        if (data == null)
        {
            return new();
        }

        return data["data"];
    }

    public async Task SetVmPassword(ProxmoxVmDto vm, string username, string password)
    {
        var requestData = new Dictionary<string, object>
        {
            { "username", username },
            { "password", password }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
        await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/agent/set-user-password", content);
    }

    public async Task<bool> GetAgentStatus(ProxmoxVmDto vm)
    {
        var response = await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/agent/ping", new StringContent(""));
        var data = await response.Content.ReadFromJsonAsync<ProxmoxAgentPingDto>();
        if (data == null)
        {
            return false;
        }

        return data.Message == "";
    }

    public async Task ResetVmPower(ProxmoxVmDto vm)
    {
        await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/status/reset", new StringContent(""));
    }

    public async Task RebootVm(ProxmoxVmDto vm)
    {
        await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/status/reboot", new StringContent(""));
    }

    public async Task<List<ProxmoxNetworkDeviceDto>> GetVmNetworkDevices(ProxmoxVmDto vm)
    {
        Dictionary<string, Dictionary<string, List<ProxmoxNetworkDeviceDto>>>? data = null;
        var response = await _client.GetAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/agent/network-get-interfaces");

        try
        {
            data = await response.Content.ReadFromJsonAsync<Dictionary<string, Dictionary<string, List<ProxmoxNetworkDeviceDto>>>>();
        }
        catch
        {
            Console.WriteLine($"Error while reading network devices. {vm.Name}");
        }

        if (data == null)
        {
            return new List<ProxmoxNetworkDeviceDto>();
        }

        return data["data"]["result"];
    }

    public async Task UpdateVmConfig(ProxmoxVmDto vm, int cpuCores, int ramMb)
    {
        var requestData = new Dictionary<string, object>
        {
            { "cores", cpuCores },
            { "memory", ramMb }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
        await _client.PutAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/config", content);
    }

    public async Task UpdateVmTags(ProxmoxVmDto vm, string tags)
    {
        var requestData = new Dictionary<string, object>
        {
            { "tags", tags }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
        await _client.PutAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/config", content);
    }

    public async Task AddStorage(ProxmoxVmDto vm, int sizeGb)
    {
        var requestData = new Dictionary<string, object>
        {
            { "scsi1", $"{Config.PROXMOX_DATA_STORAGE}:{sizeGb},cache=writeback,discard=on" },
        };

        var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
        await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/config", content);
    }

    public async Task<List<ProxmoxVmInfoTimeFrameIn>> GetVmTimeFrame(ProxmoxVmDto vm)
    {
        var response = await _client.GetAsync($"https://{Config.PROXMOX_ADDR}/api2/json/nodes/{vm.Node}/qemu/{vm.VmId}/rrddata?timeframe=day");
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, List<ProxmoxVmInfoTimeFrameIn>>>();
        if (data == null)
        {
            return new List<ProxmoxVmInfoTimeFrameIn>();
        }

        return data["data"];
    }

    // Storage
    public async Task<List<ProxmoxStorageDto>> GetProxmoxStorages()
    {
        return await GetProxmoxResources<ProxmoxStorageDto>("storage");
    }

    // Nodes
    public async Task<List<ProxmoxNodeDto>> GetProxmoxNodes()
    {
        return await GetProxmoxResources<ProxmoxNodeDto>("node");
    }

    public async Task<ProxmoxNodeDto?> GetProxmoxNode(string name)
    {
        List<ProxmoxNodeDto> nodes = await GetProxmoxNodes();
        return nodes.FirstOrDefault(x => x.Node == name);
    }

    public async Task<ProxmoxNodeDto?> GetProxmoxNodeForBooking()
    {
        List<ProxmoxNodeDto> nodes = await GetProxmoxNodes();
        List<ProxmoxNodeDto> availableNodes = nodes.Where(x => x.ReadyForBookings == true).ToList();
        return availableNodes.OrderBy(x => x.LoadIndex).FirstOrDefault();
    }

    // HA
    public async Task<bool> AddToHA(int vmId, bool waitTask = false)
    {
        var data = new
        {
            sid = vmId
        };

        var content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"https://{Config.PROXMOX_ADDR}/api2/json/cluster/ha/resources", content);
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            return await WaitForTaskFinish(upid);
        }

        return true;
    }

    public async Task<bool> DeleteFromHA(ProxmoxVmDto vm, bool waitTask = false)
    {
        var response = await _client.DeleteAsync($"https://{Config.PROXMOX_ADDR}/api2/json/cluster/ha/resources/{vm.VmId}");
        string upid = await ReadTaskUPID(response);

        if (waitTask)
        {
            return await WaitForTaskFinish(upid);
        }

        return true;
    }


    // Other
    private HttpClient InitHttpClient()
    {
        // Ignore SSL certificate
        HttpClientHandler handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        HttpClient client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("PVEAPIToken", $"{Config.PROXMOX_TOKEN_ID}={Config.PROXMOX_TOKEN_SECRET}");

        return client;
    }

    private async Task<string> ReadTaskUPID(HttpResponseMessage response)
    {
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        if (data == null)
        {
            return "";
        }

        return data["data"];
    }

    private async Task<bool> WaitForTaskFinish(string upid, int timeoutSeconds = 300)
    {
        ProxmoxTaskDto? task = await GetProxmoxTaskByUPID(upid);
        for (int i = 0; i < timeoutSeconds; i++)
        {
            await Task.Delay(2000);

            if (task == null)
            {
                Console.WriteLine($"{upid}\tTask is null");
                return false;
            }

            if (task.IsFinished == true && task.Successful == false)
            {
                Console.WriteLine($"{upid}\tTask is not successful");
                return false;
            }

            if (task.IsFinished == true && task.Successful == true)
            {
                return true;
            }
            task = await GetProxmoxTaskByUPID(upid);
        }

        Console.WriteLine($"{upid}\tTask timeout error");
        return false;
    }
}