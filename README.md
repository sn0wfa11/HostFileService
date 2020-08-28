# HostFileService

This service is designed to be used in conjunction with Group Policy to manage the Windows host file.

## Installation Instructions
Download and run the latest release installer (MSI). It will automatically start the service on install.

The installer is provided as an MSI for easier install and removal with System Configuration Center (SCCM).

## Uninstall Instructions
Uninstall HostFileService as you would any other Windows application.

## Management
On the first run of the service, it will merge any entries in the host file with any registry entries in the location below, then write all entries into both the host file and the registry location. This allows you to pre-stage host entries in the registry location with Group Policy before installing the service while maintaining any entries already in the host file. This is convenient if you apply group policy and SCCM installation of this service automatically using AD Security Groups.

Host registry entries are placed into the following registry location:

`\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\HostFileService\hosts\`

Entries are string entries with the entry name in the format of `<IP>,<Host>`, no data is needed or used for the entry.

`192.168.1.1,router`

As you add, remove, or change host registry entries the service will update the host file on its next interval.

***Once running, any changes manually made to the host file will be reverted to the registry host entries on the next service interval.***

### Setting Service Update Interval

You can set the interval value **(in minutes)** by changing the value in the following registry entry. This value can also be pre-staged by GP.

`\HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\HostFileService\Interval`
