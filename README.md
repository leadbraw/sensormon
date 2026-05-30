<div align="center">
<pre>

███████╗███████╗███╗   ██╗███████╗ ██████╗ ██████╗ ███╗   ███╗ ██████╗ ███╗   ██╗
██╔════╝██╔════╝████╗  ██║██╔════╝██╔═══██╗██╔══██╗████╗ ████║██╔═══██╗████╗  ██║
███████╗█████╗  ██╔██╗ ██║███████╗██║   ██║██████╔╝██╔████╔██║██║   ██║██╔██╗ ██║
╚════██║██╔══╝  ██║╚██╗██║╚════██║██║   ██║██╔══██╗██║╚██╔╝██║██║   ██║██║╚██╗██║
███████║███████╗██║ ╚████║███████║╚██████╔╝██║  ██║██║ ╚═╝ ██║╚██████╔╝██║ ╚████║
╚══════╝╚══════╝╚═╝  ╚═══╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═╝     ╚═╝ ╚═════╝ ╚═╝  ╚═══╝
containerized hardware monitoring on home server
</pre>
</div>

A worker made to run in [k3s](https://k3s.io/) on my home server. Periodically polls the endpoint created by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) on my desktop, and writes the data to a Postgres db, also containerized.