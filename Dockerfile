FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update && apt-get install -y \
    clang \
    lld \
    zlib1g-dev \
    wget \
    tar

# Download the aarch64-musl cross-toolchain
RUN wget -qO- https://musl.cc/aarch64-linux-musl-cross.tgz | tar -xz -C /opt/

# Add the cross-tools to PATH
ENV PATH="/opt/aarch64-linux-musl-cross/bin:${PATH}"

WORKDIR /src
