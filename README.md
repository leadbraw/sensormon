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

A worker made to run in [k3s](https://k3s.io/) on my home server. Periodically polls the endpoint created by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) on my desktop, and writes the data to a Postgres db, also containerized. Also a CI/CD workflow. On commit push, a github workflow builds a new image, pushes it to GHCR and updates the proper image tag in kustomization.yaml. [Argo CD](https://argoproj.github.io/cd/) then reconciles the cluster to match.

## TODO
- Add test stage to ci/cd pipeline
- Add Redis for event-driven alerting (worker fires event when some temps get too high, a seperate service subscribes and notifies me somehow)
- General cleanup
