.PHONY: start

start:
	Milvus\Claude\standalone-claude.bat start
	Milvus\Gemini\standalone-gemini.bat start
	Milvus\Mistral\standalone-mistral.bat start
	Milvus\OpenAi\standalone-openai.bat start

delete:
	make stop
	Milvus\Claude\standalone-claude.bat delete
	Milvus\Gemini\standalone-gemini.bat delete
	Milvus\Mistral\standalone-mistral.bat delete
	Milvus\OpenAi\standalone-openai.bat delete

stop:
	Milvus\Claude\standalone-claude.bat stop
	Milvus\Gemini\standalone-gemini.bat stop
	Milvus\Mistral\standalone-mistral.bat stop
	Milvus\OpenAi\standalone-openai.bat stop