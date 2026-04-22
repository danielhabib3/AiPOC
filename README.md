# AiPOC

## Overview

This project contains two main functionnalities:
1. **Intelligent Faq**: A system that allows users to ask questions in natural language and receive accurate answers based on an existing Faq (Questions and answers).
2. **Intelligent Search**: A system that allows users to search for a text from a list. By typing a text in natural language, the system will return the most relevant text from the list.

In the Program.cs there are two functions, one that runs a swagger with two routes, one for each functionality. The other function is a console application that allows users to test the two functionalities by typing their questions or search queries directly in the console.


## Guide to start the project

To start the project, follow these steps:
1. Clone the repository to your local machine.
2. Tap the command 'make start'. This command will create the Milvus vector databases via Docker containers. One database for each LLM.
3. You can run the project in two ways:
   - **Swagger**: Run the project using IIS Express or run the command `dotnet run`. Open your browser and navigate to `http://localhost:5256/swagger`. You will see two routes, one for each functionality. You can test the functionalities by sending requests to these routes.
   - **Console Application**: Open a terminal and navigate to the project directory. Run one of these commands `dotnet run faq-milvus`, `dotnet run suggestion-milvus` or `dotnet run suggestion-without-milvus`.
	- `dotnet run faq-milvus` for the Intelligent Faq functionality using Milvus vector database.
	- `dotnet run suggestion-milvus` for the Intelligent Search functionality using Milvus vector database.
	- `dotnet run suggestion-without-milvus` for the Intelligent Search functionality without using Milvus vector database.
