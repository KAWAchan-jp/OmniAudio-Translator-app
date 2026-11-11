FROM mcr.microsoft.com/dotnet/sdk:8.0

# Windows の DrvFS マウント上で作るファイルを、WSL 側の作業ユーザーと同じ UID/GID にする。
ARG USERNAME=devuser
ARG USER_UID=1000
ARG USER_GID=1000

RUN groupadd --gid ${USER_GID} ${USERNAME} \
    && useradd --uid ${USER_UID} --gid ${USER_GID} --create-home --shell /bin/bash ${USERNAME}

USER ${USERNAME}
