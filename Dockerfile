# ==============================================================================
# STAGE 1: Build BeaEngine (Native Library for ConfuserEx control flow cleaning)
# ==============================================================================
FROM ubuntu:22.04 AS beaengine-builder
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    git \
    cmake \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
RUN git clone --depth 1 --branch v5.3.0 https://github.com/BeaEngine/beaengine.git

RUN cmake -S beaengine -B build64 \
    -DoptBUILD_DLL=ON \
    -DoptHAS_OPTIMIZED=ON \
    -DoptHAS_SYMBOLS=OFF

RUN cmake --build build64

# Dynamically locate and move the compiled .so to a standardized path
RUN mkdir -p /app && \
    SO_FILE=$(find build64 -name "libBeaEngine*.so" | head -n 1) && \
    if [ -n "$SO_FILE" ]; then cp "$SO_FILE" /app/libBeaEngine.so; else echo "Error: libBeaEngine.so not found!" && exit 1; fi

# ==============================================================================
# STAGE 2: Build de4dotEx (.NET 8.0 Cross-Platform Release)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-builder
WORKDIR /src

# Copy all files to build
COPY . .

# Publish de4dot targeting net8.0
RUN dotnet publish -c Release -f net8.0 -o /app/publish/de4dot de4dot/de4dot.csproj
RUN rm -rf /app/publish/**/*.pdb /app/publish/**/*.xml

# ==============================================================================
# STAGE 3: Final Runtime Image (.NET 8.0 on Ubuntu / Runtime)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Install native dependencies required for execution (e.g. globalization)
RUN apt-get update && apt-get install -y \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy de4dot published folder
COPY --from=dotnet-builder /app/publish/de4dot ./de4dot

# Copy compiled libBeaEngine.so from Stage 1 standardized path to system libraries & register
COPY --from=beaengine-builder /app/libBeaEngine.so /usr/local/lib/libBeaEngine.so
RUN ldconfig

# Create symlink for global accessibility
RUN ln -s /app/de4dot/de4dot /usr/local/bin/de4dot

ENTRYPOINT ["de4dot"]
CMD ["--help"]
