# KI-Dokumentenassistent für Verwaltungsakten

![CI](https://github.com/talaalhussainn8n/AI-Document-Assistant/actions/workflows/ci.yml/badge.svg)

Eine ASP.NET-Core-Anwendung, die Dokumente einer Verwaltungsakte automatisch verarbeitet:

1. **Klassifikation**: erkennt den Dokumenttyp (Antrag, Bescheid, Rechnung, Nachweis, Vertrag).
2. **Informationsextraktion**: liest Datum, Aktenzeichen, Absender und Betrag aus.
3. **Fragen an die Akte (RAG)**: beantwortet Fragen auf Basis der hochgeladenen Dokumente, immer mit Quellenangabe.
4. **Vollständigkeitsprüfung**: meldet, welche Unterlagen für einen Vorgang (z. B. Wohngeld) noch fehlen.

## Architektur

```
Upload (PDF/TXT)
   |
   v
Textextraktion (PdfPig)
   |
   +--> Klassifikation  (Sprachmodell, Rückfall: gewichtete Schlüsselbegriffe)
   +--> Extraktion      (reguläre Ausdrücke für deutsche Formate)
   +--> Chunking + Embedding --> Vektorspeicher
                                     |
Frage --> Embedding --> Ähnlichkeitssuche --> Sprachmodell mit Quellen --> Antwort
```

| Projekt | Inhalt |
| --- | --- |
| `src/DocumentAssistant.Core` | Fachlogik ohne Web-Abhängigkeiten: Klassifikation, Extraktion, Chunking, Embeddings, Vektorsuche, RAG, Vollständigkeit |
| `src/DocumentAssistant.Api` | ASP.NET Core Minimal API, Swagger, einfache Web-Oberfläche |
| `tests/DocumentAssistant.Tests` | Unit-Tests und End-to-End-Tests der API (xUnit, WebApplicationFactory) |

### Designentscheidungen

- **Läuft auch ohne Sprachmodell.** Ist kein LLM konfiguriert oder schlägt der Aufruf fehl, greifen regelbasierte Verfahren. So bleibt die Anwendung nachvollziehbar und testbar.
- **Anbieterunabhängig.** Der LLM-Client spricht die OpenAI-kompatible API. Damit funktionieren OpenAI und lokale Modelle über Ollama, ohne zusätzliches SDK.
- **Austauschbarer Vektorspeicher.** `IVectorStore` ist im Arbeitsspeicher umgesetzt und kann durch eine Datenbank wie PostgreSQL mit pgvector ersetzt werden.
- **Antworten mit Quellen.** Der Prompt erlaubt nur Informationen aus den gefundenen Abschnitten, damit Antworten überprüfbar bleiben.

## Starten

Voraussetzung: .NET 8 SDK.

```bash
dotnet run --project src/DocumentAssistant.Api
```

Danach im Browser `http://localhost:5000` (Oberfläche) oder `/swagger` (API-Dokumentation) öffnen. Beispieldokumente liegen in `samples/`.

### Mit Sprachmodell

```bash
# OpenAI
export Llm__BaseUrl=https://api.openai.com/v1
export Llm__ApiKey=sk-...

# oder lokal mit Ollama
export Llm__BaseUrl=http://localhost:11434/v1
export Llm__ChatModel=llama3.1
```

### Mit Docker

```bash
docker build -t ai-document-assistant .
docker run -p 8080:8080 ai-document-assistant
```

## Tests

```bash
dotnet test tests/DocumentAssistant.Tests
```

## API

| Methode | Pfad | Zweck |
| --- | --- | --- |
| POST | `/api/cases/{caseId}/documents` | Dokument hochladen und analysieren |
| GET | `/api/cases/{caseId}/documents` | Dokumente einer Akte |
| GET | `/api/documents/{id}` | Einzelnes Dokument |
| POST | `/api/ask` | Frage stellen (`{ "question": "...", "caseId": "..." }`) |
| GET | `/api/cases/{caseId}/completeness?caseType=Wohngeld` | Vollständigkeit prüfen |

## Mögliche Erweiterungen

- PostgreSQL mit pgvector als dauerhafter Speicher
- Embeddings über ein Sprachmodell statt lokalem Feature Hashing
- OCR für eingescannte Dokumente
- Konfigurierbare Vorgangstypen aus der Datenbank
