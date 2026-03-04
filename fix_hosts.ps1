$content = @"
# Copyright (c) 1993-2009 Microsoft Corp.
#
# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
#
# This file contains the mappings of IP addresses to host names. Each
# entry should be kept on an individual line. The IP address should
# be placed in the first column followed by the corresponding host name.
# The IP address and the host name should be separated by at least one
# space.
#
# Additionally, comments (such as these) may be inserted on individual
# lines or following the machine name denoted by a '#' symbol.
#
# For example:
#
#      102.54.94.97     rhino.acme.com          # source server
#       38.25.63.10     x.acme.com              # x client host

# localhost name resolution is handled within DNS itself.
127.0.0.1    localhost
#	::1             localhost

127.0.0.1 www.techsmith.com
127.0.0.1 activation.cloud.techsmith.com
127.0.0.1 oscount.techsmith.com
127.0.0.1 updater.techsmith.com
127.0.0.1 camtasiatudi.techsmith.com
127.0.0.1 tsccloud.cloudapp.net
127.0.0.1 assets.cloud.techsmith.com

# --- ZoroKit START ---
127.0.0.1	zorokit.app
127.0.0.1	leos.com
# --- ZoroKit END ---
"@

$tempFile = "$env:TEMP\hosts_zorokit_clean.txt"
$content | Out-File -FilePath $tempFile -Encoding ASCII
Copy-Item -Path $tempFile -Destination "C:\Windows\System32\drivers\etc\hosts" -Force
Remove-Item $tempFile
Write-Host "Hosts dosyasi temizlendi!" -ForegroundColor Green
