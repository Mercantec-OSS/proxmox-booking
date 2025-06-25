public class VmService(ProxmoxApiService proxmoxApiService, IServiceScopeFactory scopeFactory)
{
    public async Task Book(string vmName, string templateName, string username = "", string password = "")
    {
        int vmId = await GetFreeVmId();

        // change name to vmId to use proxmox id
        vmName = await ChangeVmName(vmName, vmId);

        ProxmoxVmDto? template = await proxmoxApiService.GetTemplateByNameAsync(templateName);
        ProxmoxNodeDto? node = await proxmoxApiService.GetProxmoxNodeForBooking();

        if (node == null)
        {
            throw new Exception("Not available nodes");
        }

        if (template == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Template not found");
        }

        if (await proxmoxApiService.CloneVm(vmId, vmName, template, node, true) == false)
        {
            throw new Exception("Failed to clone vm");
        }

        await proxmoxApiService.AddToHA(vmId);
        await WaitForAgent(vmName);

        using var scope = scopeFactory.CreateScope();
        EmailService emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
        VmBookingRepository vmBookingRepository = scope.ServiceProvider.GetRequiredService<VmBookingRepository>();
        VmService vmService = scope.ServiceProvider.GetRequiredService<VmService>();
        VmBooking? vmBooking = await vmBookingRepository.GetByNameAsync(vmName);
        ProxmoxVmDto proxmoxVmDto = await vmService.GetVmByNameAsync(vmName);

        if (vmBooking == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Vm {vmName} booking not found");
        }

        ProxmoxVmDto? vm = await proxmoxApiService.GetVmByNameAsync(vmName);
        if (vm == null)
        {
            EmailDto emailError = EmailDto.GetVmError(vmBooking);
            await emailService.SendAsync(emailError);
            throw new HttpException(HttpStatusCode.NotFound, "Vm not found");
        }

        if (username != "")
        {
            await proxmoxApiService.SetVmPassword(vm, username, password);
        }

        // update vm tags
        await proxmoxApiService.UpdateVmTags(vm, "booking");

        EmailDto email = EmailDto.GetVmReady(vmBooking);
        await emailService.SendAsync(email);
    }

    public async Task<List<TemplateGetDto>> GetTemplates()
    {
        List<ProxmoxVmDto> proxmoxTemplates = await proxmoxApiService.GetProxmoxTemplates();
        return proxmoxTemplates.ConvertAll(x => TemplateGetDto.MakeGetDTO(x.Name));
    }

    public async Task<ProxmoxVmDto> GetTemplateByNameAsync(string name)
    {
        ProxmoxVmDto? template = await proxmoxApiService.GetTemplateByNameAsync(name);
        if (template == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Template not found");
        }

        return template;
    }

    public async Task<ProxmoxVmDto> GetVmByNameAsync(string name)
    {
        ProxmoxVmDto? vm = await proxmoxApiService.GetVmByNameAsync(name);
        if (vm == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Vm not ready to use or not found. Try again later");
        }

        return vm;
    }

    public async Task<VmInfoGetDto> GetVmInfo(VmBooking booking)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(booking.Name);

        // search internal ip (starts with 10.133.x.x)
        List<ProxmoxNetworkDeviceDto> devices = await proxmoxApiService.GetVmNetworkDevices(vm);
        string ip = "";
        foreach (var device in devices)
        {
            foreach (var ipAddr in device.IpAddresses)
            {
                if (ipAddr.IsIpv4 && ipAddr.IpAddress.StartsWith("10.133."))
                {
                    ip = ipAddr.IpAddress;
                    break;
                }
            }
        }

        return new()
        {
            Ip = ip,
            Name = vm.Name,
            Username = booking.Login,
            Password = booking.Password,
            Cpu = vm.MaxCPU,
            Ram = vm.MaxRamGb,
            PowerStatus = vm.Status
        };
    }

    public async Task<List<ProxmoxVmInfoTimeFrameIn>> GetVmInfoTimeFrame(VmBooking booking)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(booking.Name);

        List<ProxmoxVmInfoTimeFrameIn> frameVmInfo = await proxmoxApiService.GetVmTimeFrame(vm);
        return frameVmInfo.Where(frame => frame.MaxCpu != 0).ToList();
    }

    public async Task Remove(string vmName)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);

        for (int i = 0; i < 10; i++)
        {
            await proxmoxApiService.DeleteFromHA(vm);
            await proxmoxApiService.StopVm(vm, true);
            await proxmoxApiService.DeleteVm(vm);

            await Task.Delay(5000);

            // check if vm is deleted
            var booking = await proxmoxApiService.GetVmByNameAsync(vmName);
            if (booking == null)
            {
                break;
            }
        }
    }

    public async Task ResetPower(string vmName)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);
        await proxmoxApiService.ResetVmPower(vm);
    }

    public async Task RebootVm(string vmName)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);
        await proxmoxApiService.RebootVm(vm);
    }

    public async Task<List<ProxmoxIsoDto>> GetPromoxIsoList()
    {
        List<ProxmoxNodeDto> nodes = await proxmoxApiService.GetProxmoxNodes();
        if (nodes.Count == 0)
        {
            throw new Exception("No nodes found. Can not get iso list");
        }

        return await proxmoxApiService.GetProxmoxIsoList(nodes.First());
    }

    public async Task<List<string>> GetIsoList()
    {
        List<ProxmoxIsoDto> proxmoxIsos = await GetPromoxIsoList();
        return proxmoxIsos.Select(iso => iso.Name).ToList();
    }

    public async Task<ProxmoxIsoDto> GetProxmoxIsoByName(string name)
    {
        List<ProxmoxIsoDto> proxmoxIsoList = await GetPromoxIsoList();
        ProxmoxIsoDto? iso = proxmoxIsoList.FirstOrDefault(iso => iso.Name == name);
        if (iso == null)
        {
            throw new Exception("Iso not found");
        }

        return iso;
    }

    public async Task AttachIso(string vmName, string isoName)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);
        ProxmoxIsoDto iso = await GetProxmoxIsoByName(isoName);
        await proxmoxApiService.AttachIso(vm, iso);
    }

    public async Task DetachIso(string vmName)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);
        await proxmoxApiService.DetachIso(vm);
    }

    public async Task AddStorage(string vmName, int sizeGb)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);
        ProxmoxVmConfigDto vmConfig = await proxmoxApiService.GetVmConfig(vm);
        if (vmConfig.ExtraStorageExists)
        {
            throw new HttpException(HttpStatusCode.NotAcceptable, "Extra storage already exists");
        }

        _ = proxmoxApiService.AddStorage(vm, sizeGb);
    }

    public async Task UpdateVmResources(string vmName, int cpu, int ramMb)
    {
        ProxmoxVmDto vm = await GetVmByNameAsync(vmName);
        await proxmoxApiService.UpdateVmConfig(vm, cpu, ramMb);

        await proxmoxApiService.StopVm(vm, true);
        Thread.Sleep(5000);
        await proxmoxApiService.StartVm(vm);
    }

    public async Task<ClusterInfoDto> GetClusterInfo()
    {
        ClusterInfoDto clusterInfo = new ClusterInfoDto();

        List<ProxmoxNodeDto> nodes = await proxmoxApiService.GetProxmoxNodes();
        List<ProxmoxNodeDto> readyNodes = nodes.Where(node => node.ReadyForBookings).ToList();
        List<ProxmoxVmDto> vms = await proxmoxApiService.GetProxmoxVms();
        List<ProxmoxVmDto> templates = await proxmoxApiService.GetProxmoxTemplates();
        List<ProxmoxStorageDto> storages = await proxmoxApiService.GetProxmoxStorages();

        ProxmoxStorageDto? storage = storages.FirstOrDefault(storage => storage.Storage.ToLower() == Config.PROXMOX_DATA_STORAGE.ToLower());
        int usedStorage = 0;
        int totalStorage = 0;
        float storageUsagePercent = 0;
        if (storage != null)
        {
            usedStorage = storage.UsageGb;
            totalStorage = storage.TotalGb;
            storageUsagePercent = (float)Math.Round(storage.Usage, 4);
        }

        float cpuUsagePercent = (float)Math.Round(readyNodes.Average(node => node.Cpu), 4);

        float totalRam = readyNodes.Sum(node => node.MaxMem) / 1024 / 1024 / 1024;
        float usedRam = readyNodes.Sum(node => node.Mem) / 1024 / 1024 / 1024;
        float ramUsagePercent = (float)Math.Round(usedRam / totalRam, 4);

        clusterInfo.TotalHosts = nodes.Count;
        clusterInfo.ActiveHosts = readyNodes.Count(node => node.ReadyForBookings);
        clusterInfo.CpuTotal = $"{readyNodes.Sum(node => node.MaxCpu)} cores";
        clusterInfo.CpuUsage = $"{Math.Round(cpuUsagePercent * 100, 2)} %";
        clusterInfo.CpuUsagePercent = cpuUsagePercent;
        clusterInfo.RamTotal = $"{totalRam} GB";
        clusterInfo.RamUsage = $"{usedRam} GB";
        clusterInfo.RamUsagePercent = ramUsagePercent;
        clusterInfo.StorageTotal = $"{totalStorage} GB";
        clusterInfo.StorageUsage = $"{usedStorage} GB";
        clusterInfo.StorageUsagePercent = storageUsagePercent;
        clusterInfo.AmountVMs = vms.Count;
        clusterInfo.AmountTemplates = templates.Count;

        return clusterInfo;
    }

    public async Task<bool> WaitForAgent(string vmName)
    {
        bool result = false;

        for (int i = 0; i < 300; i++)
        {
            await Task.Delay(1000);

            ProxmoxVmDto? vm = await proxmoxApiService.GetVmByNameAsync(vmName);
            if (vm == null)
            {
                continue;
            }

            bool vmStatus = await proxmoxApiService.GetAgentStatus(vm);

            if (vmStatus)
            {
                result = true;
                break;
            }
        }

        return result;
    }

    private async Task<int> GetFreeVmId()
    {
        int vmId = Helpers.GetRandomNumber(10000, 99999);

        ProxmoxVmDto? vm = await proxmoxApiService.GetVmByIdAsync(vmId);
        if (vm == null)
        {
            return vmId;
        }

        return await GetFreeVmId();
    }

    private async Task<string> ChangeVmName(string vmName, int vmId)
    {
        // create scope to use db
        using var scope = scopeFactory.CreateScope();
        var vmRepository = scope.ServiceProvider.GetRequiredService<VmBookingRepository>();

        // change name to vmId to use proxmox id
        VmBooking? bookingVm = await vmRepository.GetByNameAsync(vmName);
        if (bookingVm == null)
        {
            return vmName;
        }

        // change in db
        bookingVm.Name = $"{vmId}--{bookingVm.OwnerId}--{bookingVm.Type.ToLower()}";
        await vmRepository.UpdateAsync(bookingVm);

        return bookingVm.Name;
    }
}