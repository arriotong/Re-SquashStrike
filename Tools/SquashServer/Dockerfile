# re:SquashStrike relay server. Built from the repository root (the server
# links the sim's sources in Assets/SquashSim):
#   docker build -f Tools/SquashServer/Dockerfile -t squash-server .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Assets/SquashSim Assets/SquashSim
COPY Tools/SquashServer Tools/SquashServer
RUN dotnet publish Tools/SquashServer/SquashServer.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV PORT=8080 SQUASH_DATA=/data/squash.json
EXPOSE 8080
ENTRYPOINT ["dotnet", "SquashServer.dll"]
