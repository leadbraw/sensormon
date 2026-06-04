<div align="center">
<pre>

███████╗███████╗███╗   ██╗███████╗ ██████╗ ██████╗ ███╗   ███╗ ██████╗ ███╗   ██╗
██╔════╝██╔════╝████╗  ██║██╔════╝██╔═══██╗██╔══██╗████╗ ████║██╔═══██╗████╗  ██║
███████╗█████╗  ██╔██╗ ██║███████╗██║   ██║██████╔╝██╔████╔██║██║   ██║██╔██╗ ██║
╚════██║██╔══╝  ██║╚██╗██║╚════██║██║   ██║██╔══██╗██║╚██╔╝██║██║   ██║██║╚██╗██║
███████║███████╗██║ ╚████║███████║╚██████╔╝██║  ██║██║ ╚═╝ ██║╚██████╔╝██║ ╚████║
╚══════╝╚══════╝╚═╝  ╚═══╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═╝     ╚═╝ ╚═════╝ ╚═╝  ╚═══╝
containerized hardware monitoring on my home server
</pre>
</div>

The core of the project is two .NET 10 BackgroundServices running in [k3s](https://k3s.io/) on my home server, a worker and an alerter. The worker periodically polls the endpoint created by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) on my desktop, and writes the data to a Postgres db, also containerized. If a temperature sensor exceeds a threshold defined in the config, the worker will emit an event to a Valkey stream, which the alerter will consume and send a push notification to my phone with details via [ntfy.sh](https://ntfy.sh). (The two services share a schema defining what exactly an alert should look like). A CronJob periodically prunes old readings from the database.

In addition, on commit push, a github workflow builds a new image, pushes it to GHCR and updates the proper image tag in kustomization.yaml. [Argo CD](https://argoproj.github.io/cd/) then reads/fetches the new image & reconciles the cluster to match.

## TODO
- Add test stage to ci/cd pipeline
- General cleanup
