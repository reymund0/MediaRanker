# MediaRanker

A personal media reviewing and ranking web application where users can define their own scoring criteria and compare media across different categories. Written as a modular monolith using ASP.NET Core and Next.js with AWS integrations.

## Engineering Highlights
- Modular Monolith architecture with feature-based modules for clear separation of concerns.
- Events between modules to reduce coupling and support cross-module updates.
- Integration with AWS Cognito to provide user authentication.
- Direct browser uploads to S3 for media cover images.
- ProblemDetails based API responses for consistent error handling.
- Integration tests against PostgreSQL and AWS flows using Testcontainers, Respawn, and LocalStack.

## Key Features Implemented
- Review Template CRUD with ordered scoring fields.
- Media CRUD with pre-signed S3 upload flow for cover images
- Cognito based login and server authentication.

## Future Features and Enhancements
- A personalized dashboard displaying a user's top reviews, media to revisit, and recommendations. 
- User specific head to head ranking between existing reviews.
- Export Transform and Load (ETL) pipeline for importing media from external sources like IMDb for movies or Steam for video games.

## Long Term Goals
- Establish user roles to separate Admin screens and actions from general users. 
- Extract Files module into a separate microservice utilizing AWS Lambdas and DynamoDB.

## Why This Stack

- **ASP.NET Core (.NET 9)** — To build a production style API in the .NET ecosystem while refreshing and deepening my C# experience after several years away from it.
- **Next.js 16 (React 19)** —  Chosen for its built-in routing, server rendering capabilities, and strong developer experience for React applications.
- **Modular Monolith** — To allow for simple deployment while enforcing feature boundaries and leaving room for future microservice extraction if justified.

## Architecture Overview

```
┌─────────────────────┐       ┌──────────────────────────────────┐
│  Next.js Frontend   │──────▶│  ASP.NET Core API                │
│  (React 19 / MUI)   │ REST  │  Modular Monolith (.NET 9)       │
└─────────────────────┘       │                                  │
                              │  ┌────────────┐ ┌─────────────┐  │
                              │  │ Templates  │ │   Media     │  │
                              │  └────────────┘ └─────────────┘  │
                              │  ┌────────────┐ ┌─────────────┐  │
                              │  │  Reviews   │ │   Files     │  │
                              │  └────────────┘ └─────────────┘  │
                              └──────────┬───────────┬───────────┘
                                         │           │
                              ┌──────────▼──┐  ┌─────▼──────────┐
                              │ PostgreSQL  │  │   AWS S3       │
                              │ (EF Core)   │  │ (file storage) │
                              └─────────────┘  └────────────────┘

                              ┌──────────────────────────────────┐
                              │         AWS Cognito              │
                              │  (authentication / JWT tokens)   │
                              └──────────────────────────────────┘
```

### AWS Integrations

- **Amazon Cognito** — Handles user authentication. The frontend uses AWS Amplify for sign-up/login flows; the API validates Cognito-issued JWTs via Bearer authentication.
- **Amazon S3** — Stores uploaded files (e.g. media cover images). The API generates pre-signed URLs for direct browser uploads and manages the upload lifecycle.

## Solution Structure

| Project | Description |
|---|---|
| `MediaRankerServer/` | ASP.NET Core Web API — modular monolith with feature-based modules |
| `MediaRankerFrontend/` | Next.js frontend — React 19, MUI, TanStack Query |
| `MediaRankerServer.IntegrationTests/` | Integration tests that run in docker to test PostgreSQL and AWS interactions (Testcontainers, Respawn, Localstack) |
| `MediaRankerServer.UnitTests/` | Isolated unit tests (Moq, FluentAssertions) |

## Local Development

### Prerequisites

- .NET 9 SDK
- Node.js (LTS)
- Docker (for PostgreSQL via Docker Compose)
- AWS credentials configured (Cognito + S3)

### Configuration

Before running, each project requires its own environment file:

- **API** — Copy `MediaRankerServer/appsettings.Development.json.template` to `MediaRankerServer/appsettings.Development.json` and fill in your AWS and PostgreSQL credentials.
- **Frontend** — Copy `MediaRankerFrontend/.env.example` to `MediaRankerFrontend/.env.local` and fill in your AWS credentials and API configuration.

### Running
Currently the project is set up to run the API and frontend separately as I'm mid development. In the future I plan to containerize the entire application for easy deployment.

```bash
# Start PostgreSQL
docker compose up -d postgres

# Run the API (from MediaRankerServer/)
dotnet run

# Run the frontend (from MediaRankerFrontend/)
pnpm install
pnpm run dev
```
