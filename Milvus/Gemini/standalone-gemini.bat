@echo off
setlocal enabledelayedexpansion

:main
if "%1"=="" (
    echo Please use standalone-gemini.bat restart^|start^|stop^|delete
    exit /b 1
)

if "%1"=="restart" (
    call :stop
    call :start
) else if "%1"=="start" (
    call :start
) else if "%1"=="stop" (
    call :stop
) else if "%1"=="delete" (
    call :delete
) else (
    echo Unknown command.
    echo Please use standalone-gemini.bat restart^|start^|stop^|delete
    exit /b 1
)
goto :eof

:run_embed
(
echo listen-client-urls: http://0.0.0.0:2379
echo advertise-client-urls: http://0.0.0.0:2379
echo quota-backend-bytes: 4294967296
echo auto-compaction-mode: revision
echo auto-compaction-retention: '1000'
) > embedEtcd-gemini.yaml

(
echo # Extra config to override default milvus.yaml
) > user-gemini.yaml

if not exist "embedEtcd-gemini.yaml" (
    echo embedEtcd-gemini.yaml file does not exist. Please try to create it in the current directory.
    exit /b 1
)
if not exist "user-gemini.yaml" (
    echo user-gemini.yaml file does not exist. Please try to create it in the current directory.
    exit /b 1
)

docker run -d ^
    --name standalone-milvus-gemini ^
    --security-opt seccomp:unconfined ^
    -e ETCD_USE_EMBED=true ^
    -e ETCD_DATA_DIR=/var/lib/milvus/etcd ^
    -e ETCD_CONFIG_PATH=/milvus/configs/embedEtcd-gemini.yaml ^
    -e COMMON_STORAGETYPE=local ^
    -e DEPLOY_MODE=STANDALONE ^
    -v "%cd%\volumes-gemini:/var/lib/milvus" ^
    -v "%cd%\embedEtcd-gemini.yaml:/milvus/configs/embedEtcd-gemini.yaml" ^
    -v "%cd%\user-gemini.yaml:/milvus/configs/user-gemini.yaml" ^
    -p 19532:19530 ^
    -p 9093:9091 ^
    -p 2381:2379 ^
    --health-cmd="curl -f http://localhost:9091/healthz" ^
    --health-interval=30s ^
    --health-start-period=90s ^
    --health-timeout=20s ^
    --health-retries=3 ^
    milvusdb/milvus:v2.6.11 ^
    milvus run standalone >nul

if %errorlevel% neq 0 (
    echo Failed to start Milvus container.
    exit /b 1
)

goto :eof

:wait_for_milvus_running
echo Wait for Milvus Starting...
:wait_loop
for /f "tokens=*" %%A in ('docker ps ^| findstr "standalone-milvus-gemini" ^| findstr "healthy"') do set running=1
if "!running!"=="1" (
    echo Start successfully.
    echo To change the default Milvus configuration, edit user-gemini.yaml and restart the service.
    goto :eof
)
timeout /t 1 >nul
goto wait_loop

:start
for /f "tokens=*" %%A in ('docker ps ^| findstr "standalone-milvus-gemini" ^| findstr "healthy"') do (
    echo Milvus is running.
    exit /b 0
)

for /f "tokens=*" %%A in ('docker ps -a ^| findstr "standalone-milvus-gemini"') do set container_exists=1
if defined container_exists (
    docker start standalone-milvus-gemini >nul
) else (
    call :run_embed
)

if %errorlevel% neq 0 (
    echo Start failed.
    exit /b 1
)

call :wait_for_milvus_running
goto :eof

:stop
docker stop standalone-milvus-gemini >nul
if %errorlevel% neq 0 (
    echo Stop failed.
    exit /b 1
)
echo Stop successfully.
goto :eof

:delete_container
for /f "tokens=*" %%A in ('docker ps ^| findstr "standalone-milvus-gemini"') do (
    echo Please stop Milvus service before delete.
    exit /b 1
)
docker rm standalone-milvus-gemini >nul
if %errorlevel% neq 0 (
    echo Delete Milvus container failed.
    exit /b 1
)
echo Delete Milvus container successfully.
goto :eof

:delete
set /p check="Please confirm if you'd like to proceed with the delete. This operation will delete the container and data. Confirm with 'y' for yes or 'n' for no. > "
if /i "%check%"=="y" (
    call :delete_container
    rmdir /s /q "%cd%\volumes-gemini"
    del /q embedEtcd-gemini.yaml
    del /q user-gemini.yaml
    echo Delete successfully.
) else (
    echo Exit delete
    exit /b 0
)
goto :eof

:EOF