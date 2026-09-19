# Flow - Challenge Águia Branca (Sprint 2)

O **Flow** é uma plataforma corporativa de gestão do ciclo de vida da inovação, construída para a Águia Branca. O sistema conecta dores operacionais a ideias da linha de frente, transforma ideias aprovadas em projetos rastreáveis e monitora resultados de negócio (ROI) — tudo potencializado por Inteligência Artificial (Google Gemini).

## 🏢 Arquitetura e Tecnologias

A aplicação foi estruturada utilizando **Clean Architecture** e **Domain-Driven Design (DDD)**.
* **Backend:** C# / .NET 8 (Web API)
* **Banco de Dados:** MongoDB (NoSQL)
* **Frontend Mobile:** React Native / Expo
* **Inteligência Artificial:** Google Gemini API
* **Infraestrutura:** Docker & Docker Compose

---

## 🚀 Passo 1: Executando o Backend (API + Banco de Dados)

O backend deve estar rodando para que o aplicativo mobile (via Emulador ou Expo Go) consiga se comunicar.

1. Na raiz do projeto, renomeie o arquivo `.env.example` para `.env` (ou utilize o arquivo `appsettings.json` em `src/Flow.API/`).
2. Insira uma chave válida do Google Gemini na variável `GEMINI_API_KEY` para habilitar a IA. *(Sem a chave, os endpoints inteligentes retornam 503, mas o CRUD funciona normalmente).*
3. No terminal, na raiz do projeto, execute:
   ```bash
   docker-compose up -d --build
   ```
4. A API estará disponível. Você pode explorar os endpoints e testar a aplicação acessando o Swagger em: `http://localhost:5153/swagger`

---

## 📱 Passo 2: Executando o Aplicativo Mobile (Escolha A ou B)

Para facilitar a correção, disponibilizamos duas formas de testar o frontend:

### Opção A: Utilizando o APK (Recomendado via Emulador Android)
Anexamos na entrega um arquivo `.apk` já compilado. Ele está configurado internamente para apontar para o IP `10.0.2.2`, que é a ponte de rede padrão de emuladores para o `localhost` da máquina.
1. Garanta que o backend (Docker) está rodando na sua máquina.
2. Abra o seu Emulador Android (ex: via Android Studio).
3. Arraste e solte o arquivo `.apk` para dentro do emulador para instalar.
4. Abra o aplicativo "Flow" e utilize normalmente.

### Opção B: Utilizando o Expo Go (Dispositivo Físico)
Se preferir rodar direto do código-fonte para testar em um aparelho físico:
1. Acesse o diretório do frontend: `cd mobile`
2. Crie ou edite o arquivo `.env` na raiz da pasta `mobile`, configurando o IP da máquina hospedeira da API (não use `localhost`):
   ```env
   EXPO_PUBLIC_API_URL=http://<SEU_IP_LOCAL>:5153
   ```
3. Instale as dependências: `npm install`
4. Inicie o Metro Bundler: `npx expo start -c`
5. Escaneie o QR Code com o aplicativo **Expo Go** no seu celular (conectado à mesma rede Wi-Fi do computador).

---

## 🧠 Diferencial de Inovação (IA com Google Gemini)

O Flow utiliza o modelo **Gemini 1.5 Flash** integrado na camada de infraestrutura. A inteligência artificial atua como motor de decisão:
- **Insights Executivos (Dashboard):** Lê a base de resultados e prazos dos projetos concluídos e gera análises consolidadas e alertas de gargalos (ex: bloqueios em projetos) diretamente para a liderança.