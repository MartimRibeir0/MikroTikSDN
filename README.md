# MikroTikSDN

Aplicação Windows Forms para gerir vários routers **MikroTik** a partir de um único painel, através da **REST API do RouterOS v7**. Permite consultar e alterar a configuração de rede sem abrir o WinBox ou a consola de cada equipamento.

## Funcionalidades

- **Dispositivos**: adicionar, editar e remover routers (host, porta, HTTP/HTTPS, credenciais) e alternar entre eles.
- **Interfaces**: listagem e estado das interfaces.
- **Wireless**: configuração das interfaces sem fios e gestão de perfis de segurança.
- **Bridge**: criação de bridges e gestão das portas associadas.
- **IP**: endereços IP e rotas estáticas.
- **DHCP**: servidores, pools e redes DHCP.
- **DNS**: definições do servidor DNS e entradas estáticas.
- **Sistema**: informação do equipamento e nome DNS do MikroTik Cloud.
- **WireGuard**: interfaces e peers, geração de chaves no cliente, assistente de configuração rápida de VPN e geração do ficheiro de configuração do cliente.

## Tecnologias

C#, .NET 8 (Windows Forms), Newtonsoft.Json, Chaos.NaCl (chaves Curve25519 do WireGuard).

## Requisitos

- Windows com .NET 8 SDK
- Router MikroTik com RouterOS 7.1 ou superior e o serviço `www` ou `www-ssl` ativo

## Executar

```bash
dotnet run --project MikroTikSDN.csproj
```

Ou abrir `MikroTikSDN.sln` no Visual Studio e executar.

## Estrutura

```text
Api/MikroTikClient.cs     Cliente HTTP da REST API (GET/PUT/PATCH/POST/DELETE)
Services/Services.cs      Serviços por área: interfaces, wireless, bridge, IP, rotas, DHCP, DNS, WireGuard
Models/                   Modelos do RouterOS e gestão dos dispositivos guardados
Forms/MainForm.cs         Janela principal e navegação
Forms/Panels.cs           Painéis e formulários de cada área
```

## Segurança

- Os dispositivos são guardados em `%APPDATA%\MikroTikSDN\devices.json`. As palavras-passe ficam **em texto simples** nesse ficheiro.
- A aplicação aceita qualquer certificado HTTPS, para funcionar com os certificados autoassinados dos routers. Usar apenas em redes de confiança ou de laboratório.
