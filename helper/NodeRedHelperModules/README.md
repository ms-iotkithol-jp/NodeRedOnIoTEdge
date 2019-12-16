# Helper Modules 
Node-Red を IoT Edge Runtime 上で動作させるときに便利なヘルパーモジュール群 
基本的な使い方は、 
1. VS Code で helper/NODEREDHELPERMODULE を開く。 
2. .env ファイルを、各自の [Azure Container Registry](https://docs.microsoft.com/ja-jp/azure/container-registry/) に合わせて更新する。
3. VS Code の Azure IoT Edge Extension で、Build と Azure Container Registry へのアップロードを行う
4. それぞれの Module の説明を参考に、 IoT Edge デバイスに配置する　

# File Sync Helper Module 
同一の IoT Edge Runtime 上で動作している、IoT Edge Module にマウントされているディレクトリに存在するファイルを Cloud 側の Blob Storage に転送、または、ファイルの内容を置き換える。 
## 使い方 
[Blob on Edge](https://docs.microsoft.com/ja-jp/azure/iot-edge/how-to-access-host-storage-from-module) と組み合わせて使用する。 
[deployment.template.json](./deployment.template.json) を参考に(<...>の部分は適宜自分の環境に合わせて書き換えること）配置を指定するとよい。Azure ポータルで指定する場合には、createOptionsの直下の、
```json
{
    "HostConfig": {
        "Binds": [
            "< directory name on host side >:< mounted directory name in container >"
        ]
    }
}
```
の部分を”コンテナ―の作成オプション”に書き込み、
```json
"env": {
    "BLOB_ON_EDGE_MODULE": {
        "value": "localblobstorage"
    },
    "BLOB_ON_EDGE_ACCOUNT_NAME": {
        "value": "< Account Name of Blob on Edge - should be same text for LOCAL_STORAGE_ACCOUNT_NAME! >"
    },
    "BLOB_ON_EDGE_ACCOUNT_KEY": {
        "value": "< Account Key of Blob on Edge -should be same text for LOCAL_STORAGE_ACCOUNT_KEY! >"
    },
    "BLOB_ON_EDGE_CONTAINER_NAME": {
        "value": "< Container Name for Cloud syncing >"
    },
    "RESTAPI_SERVER_MODULE_NAME": {
        "value": "< Edge Module Name providing REST API Service - optional >"
    },
    "RESTAPI_SERVER_MODULE_PORT": {
        "value": "< Edge Module Port providing REST API Service - optional >"
    }
}

```
の部分は、面倒くさいけど、一個づつ、環境変数に設定すること。  
尚、このモジュールは、embeddedgeorge/file-sync-helper:0.1.0-arm32v7 で利用可能

### Module Direct Method 
#### UploadFileToCloud 
指定したファイルを、Blob on Edge の指定されたフォルダーに Upload する。Blob on Edge が、Azure Blob Storage へのファイル同期が設定されていれば、その設定に従い、Blob on Edge に Upload されたファイルが、Azure Blob Storage に転送される。  

payload : {"filename":"<i>uploading-file-name</i>"}  

ファイルの名前は以下のフォーマットで Blob on Edge に Upload される
<i>deviceId</i>-<i>yyyyMMddHHmmss</i>-<i>uploading-file-name</i>  
※ yyyy…の部分は、Upload時の日時  
例） payloadで、"/data/flows.json" を指定した場合、Device Id が raspberrypi で、 2019年12月13日 16時28分49秒だった場合は、"raspberrypi-20191213162849-flows.json" という名前で Upload される。また、/data/の部分は、"createOptions" で、"Binds" 指定して、他の Module も含め共有できるようにしておくこと。   

※ 本機能を使うには、環境変数の BLOB_ON_EDGE_MODULE、BLOB_ON_EDGE_ACCOUNT_NAME、BLOB_ON_EDGE_ACCOUNT_KEY、BLOB_ON_EDGE_CONTAINER_NAME を設定し、Blob on Edge にアクセスできるようにすること  

#### ReplaseFileFromCloud  
指定したファイルの内容を書き換える。 

palyload : {"filename":"<i>replacing-file-name</i>","content":"<i> any text </i>"}  

<i>replacing-file-name</i> で指定したファイルが、 <i> any text </i> で書き変わる。 

#### InvokeRESTService 
同じ Runtime 上で動いている Module が公開している HTTP REST API をコールし、結果を返す。 

payload : {"method":"<i>GET</i> or <i>POST</i>","uri":"<i>request uri</i>","body":"<i>content of body in the case POST</i>","header":["<i>header-key</i>:<i>header-value</i>",...]}

※ 本機能を利用する場合には、環境変数の、RESTAPI_SERVER_MODULE_NAME、RESTAPI_SERVER_MODULE_PORT を設定すること。また、指定された REST API を提供する Module は、対応するHTTP REST API サービスを提供すること。  
例えば、 

- RESTAPI_SERVER_MODULE_NAME => "TibboPiIoTModule"
- RESTAPI_SERVER_MODULE_PORT => "1880"  

<B> Under Construction </B>