# Milvus — Technical Documentation

## Overview

Milvus is a vector DB open source project that allows users to store and manage large amounts of vector data. It provides a powerful and efficient way to perform similarity search and other operations on vector data.

In this folder there is a DB for each LLM used to vectorize the data. In each folder there is a standalone.bat file that have 3 commands:
- `./standalone.bat start` to start the container with the DB or create it if it does not exist yet and installs the image if not installed
- `./standalone.bat stop` to stop the container with the DB
- `./standalone.bat delete` to delete the container with the DB and all the data stored in it.

To add a LLM to the project, you need to :
1. Create a new folder with the name of the LLM 
2. Add a standalone.bat file with the same commands as the other standalone.bat files
3. Rename the file standalone.bat to standalone-LLMName.bat and add it to the Makefile.
4. In the standalone.bat file:
	- Rename embedEtcd.yaml to embedEtcd-LLMName.yaml
	- Rename volumes to volumes-LLMName
	- Rename user.yaml to user-LLMName.yaml
5. It's important to change the port on which the container runs and add it to the appsetting.json.
6. Add Api key and Url of the LLM to the appsetting.json file.
7. You also have to add an implementation of the class EmbeddingProvider and override the abstract methods of the class.