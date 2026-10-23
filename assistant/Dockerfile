FROM python:3.12-slim
WORKDIR /app
COPY assistant/requirements.lock /app/requirements.lock
RUN pip install --no-cache-dir -r requirements.lock
COPY assistant/ /app/
RUN pip install --no-cache-dir --no-deps . && useradd --create-home workbench && mkdir -p /app/.state && chown -R workbench /app
USER workbench
EXPOSE 8768
CMD ["uvicorn", "fishpeek.api:create_app", "--factory", "--host", "0.0.0.0", "--port", "8768"]
