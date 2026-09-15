# Deploy

Como subir o Flow: local, em containers e em produção com Dokploy e Traefik.

---

## 1. Pré-requisitos

| Item | Versão | Observação |
|---|---|---|
| .NET SDK | 8.0 | |
| MongoDB | 8.0 | **Replica set obrigatório** |
| Node | 20+ | Apenas para o mobile |
| Docker | 24+ | Opcional para desenvolvimento; necessário para a imagem |

### Por que o replica set não é opcional

O Flow grava o agregado, a entrada de auditoria e o snapshot do projeto dentro de **uma
transação multi-documento**, e transações não existem em um `mongod` standalone. Um Mongo
avulso sobe, aceita leitura e escrita e parece funcionar — até a primeira transição de
projeto falhar.

Em desenvolvimento e em teste, um replica set de **nó único** é suficiente e é o que o
`docker-compose.yml` cria.

---

## 2. Local, sem Docker

```bash
# 1. MongoDB como replica set de nó único
mongod --replSet rs0 --dbpath ./data --port 27017 --bind_ip 127.0.0.1

# 2. iniciar o conjunto (uma única vez)
mongosh --eval 'rs.initiate({_id:"rs0", members:[{_id:0, host:"127.0.0.1:27017"}]})'
mongosh --eval 'rs.status().myState'   # 1 = PRIMARY

# 3. configuração
cp .env.example .env     # preencha JWT_SECRET_KEY

# 4. API
export JwtSettings__SecretKey="$(openssl rand -base64 48)"
export SEED_DEMO_DATA=true
export SEED_DEMO_PASSWORD='FlowDemo!2026'
dotnet run --project src/Flow.API
```

A API sobe em `http://localhost:5153`, com Swagger em `/swagger`.

No startup ela cria os índices de forma idempotente e garante os três papéis do Identity.

### Mobile

```bash
cd mobile
npm ci
npx expo start
```

A URL da API é resolvida sozinha: em desenvolvimento o app usa o host que serve o bundle,
então um aparelho na mesma rede encontra a máquina sem nenhuma edição.

---

## 3. Docker Compose

```bash
cp .env.example .env      # JWT_SECRET_KEY é obrigatório
docker compose up -d
docker compose logs -f api
```

O compose sobe dois serviços:

- **mongo** — `mongo:8.0` com `--replSet rs0`. O healthcheck executa `rs.initiate` na
  primeira subida e só reporta saudável quando o nó é PRIMARY.
- **api** — imagem multi-stage, com `depends_on: service_healthy`, de modo que a API só
  inicia quando o replica set está pronto.

### A imagem

- build em `sdk:8.0-alpine`, runtime em `aspnet:8.0-alpine`;
- restore em camada separada, então uma edição de código não reinstala pacotes;
- roda como usuário **não privilegiado**;
- `HEALTHCHECK` aponta para `/health/ready`, não `/health/live`: o orquestrador só deve
  encaminhar tráfego quando o MongoDB estiver de fato alcançável;
- `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` e `icu-libs`, necessários para a formatação
  pt-BR de moeda e data;
- **nenhum segredo na imagem** — tudo chega por variável de ambiente.

---

## 4. Produção com Dokploy e Traefik

### 4.1 MongoDB

Provisionar um replica set. Uma instância avulsa **não serve**.

```text
mongodb://<user>:<senha>@<host>:27017/?replicaSet=rs0&authSource=admin&tls=true
```

### 4.2 Aplicação no Dokploy

1. Criar uma aplicação do tipo **Docker** apontando para o repositório;
2. Build path `/`, Dockerfile `Dockerfile`;
3. Porta interna **8080**;
4. Health check path `/health/ready`.

### 4.3 Variáveis de ambiente

```bash
ASPNETCORE_ENVIRONMENT=Production

Mongo__ConnectionString=<a string acima>
Mongo__Database=flow

JwtSettings__SecretKey=<openssl rand -base64 48>
JwtSettings__Issuer=FlowAPI
JwtSettings__Audience=FlowApp
JwtSettings__ExpiryMinutes=15
Auth__RefreshTokenDays=7

Gemini__ApiKey=<chave>
Gemini__Model=gemini-3.8-flash

OneSignal__AppId=<app id>
OneSignal__ApiKey=<rest api key>

CORS_ALLOWED_ORIGINS=https://flow.example.com
Swagger__Enabled=false
SEED_DEMO_DATA=false
```

> A API **recusa iniciar** fora de Development se `JwtSettings__SecretKey` ainda for o
> placeholder ou tiver menos de 32 bytes. É deliberado: um segredo fraco em produção é a
> falha mais fácil de cometer e a mais cara de descobrir depois.

### 4.4 HTTPS com Traefik

O Dokploy já roda Traefik. Basta associar o domínio e habilitar o certificado.

```yaml
# labels equivalentes, caso configure na mão
traefik.enable: "true"
traefik.http.routers.flow.rule: "Host(`flow-api.example.com`)"
traefik.http.routers.flow.entrypoints: "websecure"
traefik.http.routers.flow.tls.certresolver: "letsencrypt"
traefik.http.services.flow.loadbalancer.server.port: "8080"
```

### 4.5 Forwarded headers — obrigatório atrás do proxy

O TLS termina no Traefik, então a aplicação recebe HTTP interno e o peer TCP é o **proxy**,
não o usuário. Sem processar os cabeçalhos encaminhados:

- `RemoteIpAddress` é o endereço do Traefik para **todo mundo**. O rate limit de `/auth/*`
  particiona por endereço, então **todos os clientes caem no mesmo balde** — o primeiro a
  errar a senha algumas vezes tranca os demais;
- `Request.Scheme` é `http` mesmo com o cliente em HTTPS, o que faz `UseHttpsRedirection`
  e qualquer URL absoluta olharem para o lado errado;
- um `RemoteIp` gravado em log ou auditoria apontaria para o proxy.

```bash
FORWARDED_HEADERS_ENABLED=true
FORWARDED_TRUSTED_NETWORKS=172.16.0.0/12   # rede do Docker onde o Traefik roda
FORWARDED_LIMIT=1                          # um proxy, um salto
```

Fora do Compose, os nomes são hierárquicos:

```bash
ForwardedHeaders__Enabled=true
ForwardedHeaders__TrustedNetworks__0=172.16.0.0/12
ForwardedHeaders__ForwardLimit=1
```

#### Por que é uma lista e não um interruptor

Um cabeçalho encaminhado é só um cabeçalho: qualquer cliente pode enviar um. Ele só vale
como prova quando a requisição **chegou de um proxy que nós operamos**, e é isso que
`TrustedProxies` e `TrustedNetworks` declaram.

O padrão do ASP.NET Core confia apenas em loopback, o que é correto para um proxy na mesma
máquina e inútil em container, onde o Traefik chega pela rede bridge. A saída fácil e
errada é limpar `KnownProxies` e `KnownNetworks` para "funcionar": isso transforma um
cabeçalho controlado pelo cliente na identidade do cliente, e qualquer um passa a escolher
o próprio endereço — inclusive para escapar do rate limit. **Não fazemos isso.** A lista
substitui o padrão de loopback por uma permissão explícita.

`ForwardLimit` fica em 1 pelo mesmo motivo: os cabeçalhos são lidos da direita para a
esquerda, e um salto significa ler apenas o valor que o nosso proxy anexou. Um cliente que
mande `X-Forwarded-For: 10.9.9.9, 203.0.113.10` não consegue fazer o `10.9.9.9` ser lido.

#### Falha explícita em vez de silenciosa

Se `FORWARDED_HEADERS_ENABLED=true` e nenhuma rede ou proxy for declarado, **a API recusa
iniciar** fora de Development. É deliberado: nessa combinação só o loopback seria confiado,
os cabeçalhos seriam ignorados, e tudo pareceria configurado enquanto todos os clientes
dividiam um balde de rate limit. É o mesmo critério aplicado ao segredo JWT.

Em Development a validação não roda, porque ali normalmente não há proxy nenhum.

O comportamento é coberto por `ForwardedHeadersTests`, inclusive o caso que mais importa:
um cliente que **não** veio pelo proxy não consegue forjar o próprio endereço nem o
esquema.

### 4.6 Depois do deploy

```bash
curl -fsS https://flow-api.example.com/health/live
curl -fsS https://flow-api.example.com/health/ready
```

`ready` verde significa que o MongoDB respondeu e os índices existem.

---

## 5. Mobile

```bash
cd mobile

# JS puro, para conferir se o projeto empacota
npx expo export --platform android

# build nativo (exige credencial EAS)
eas build --platform android --profile preview   # APK instalável
```

Perfis em `eas.json`:

| Perfil | Saída | Uso |
|---|---|---|
| `development` | APK com dev client | Depuração com API local |
| `preview` | **APK** | Distribuição interna e instalação direta |
| `production` | **APK** | Entrega |
| `production-store` | AAB | Google Play |

A URL da API vem do perfil, por `EXPO_PUBLIC_API_URL`. Ajuste antes de gerar o build.

---

## 6. Segredos

| Segredo | Onde vive | Onde **nunca** vive |
|---|---|---|
| `JwtSettings__SecretKey` | Variável de ambiente | Repositório, imagem, log |
| `Gemini__ApiKey` | Variável de ambiente, servidor | App mobile, log, `assistant_runs` |
| `OneSignal__ApiKey` | Variável de ambiente, servidor | App mobile |
| OneSignal **App ID** | Perfil EAS | — é público por natureza |
| Senha do MongoDB | Connection string por env | Repositório |
| `SEED_DEMO_PASSWORD` | Variável de ambiente, só em demo | Produção |

`.env` está no `.gitignore`. `.env.example` traz todas as chaves com valores vazios.

---

## 7. Estado de verificação

| Item | Estado |
|---|---|
| API rodando contra MongoDB 8.0 em replica set | ✅ verificado nesta máquina |
| Criação idempotente de índices no startup | ✅ verificado |
| Health checks respondendo | ✅ verificado |
| Seed de demonstração idempotente | ✅ verificado |
| Export de `openapi.json` a partir da aplicação | ✅ verificado, 54 endpoints |
| Bundle Android do mobile | ✅ verificado, 5,59 MB Hermes |
| `expo-doctor` | ✅ 18/18 |
| Build da imagem Docker | ✅ verificado **na CI**, não nesta máquina |
| `docker compose up` com replica set | ✅ verificado **na CI**, com `/health/ready` verde |
| Forwarded headers com proxy confiável | ✅ verificado por teste |
| **Deploy no Dokploy com HTTPS** | ⏳ **não executado** — falta credencial |
| **APK via EAS** | ⏳ **não executado** — falta credencial |

### Onde isso é verificado agora

O que a máquina de desenvolvimento não consegue rodar, a CI roda. O workflow
[`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) tem quatro jobs:

| Job | O que prova |
|---|---|
| **Backend** | `restore`, `build -c Release`, suíte completa contra MongoDB real via Testcontainers, e um passo que **falha se algum teste for pulado** |
| **Mobile** | `npm ci`, `tsc --noEmit`, `expo-doctor`, `expo export --platform android` |
| **Docker** | `docker compose build` e `docker compose up`, esperando o `/health/ready` — é aqui que o Dockerfile e o compose são construídos pela primeira vez |
| **Artefatos** | `build-artifacts.sh` e conferência do `openapi.json` exportado da aplicação |

Sem segredo nenhum: o `.env` do job de Docker é gerado na hora, com um valor descartável, e
`.env` não está no repositório. As permissões do workflow são `contents: read`.

### Docker: construído, só que não aqui

O Docker Desktop desta máquina não sobe — os processos iniciam, a distro WSL
`docker-desktop` fica em `Stopped` e `docker desktop status` trava. Isso não mudou.

O que mudou é que deixou de importar: o job de Docker da CI constrói a imagem e sobe o
compose inteiro a cada push, em runner limpo. A imagem passou a ser validada de verdade, e
por alguém que não sou eu com o ambiente já aquecido — o que é uma evidência melhor do que
a que eu teria produzido localmente.

Para reproduzir em uma máquina com Docker funcionando:

```bash
docker compose build
docker compose up -d
curl -fsS localhost:5153/health/ready
```

### O que continua pendente, e por quê

**Dokploy e EAS.** Dependem de credenciais que não existem neste ambiente: acesso ao
servidor Dokploy e conta EAS com credencial de assinatura Android. Nenhum dos dois pode ser
resolvido por engenharia — só por acesso.

O que **foi** verificado é o que sustenta os dois: a aplicação publica em Release, sobe
contra um MongoDB real, responde nos health checks, e o projeto mobile empacota para
Android. O que falta é execução com credencial, não código.
