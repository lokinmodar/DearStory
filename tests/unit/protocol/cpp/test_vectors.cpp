#include "test_vectors.hpp"

#include <filesystem>
#include <fstream>
#include <sstream>
#include <stdexcept>

#include <nlohmann/json.hpp>

std::string read_vector(std::string_view file_name)
{
    const auto path = std::filesystem::path(DEARSTORY_REPOSITORY_ROOT) / "protocol" / "test-vectors" / "handshake" / file_name;
    std::ifstream stream(path, std::ios::binary);
    if (!stream)
    {
        throw std::runtime_error("failed to open vector file");
    }

    std::ostringstream buffer;
    buffer << stream.rdbuf();
    return buffer.str();
}

bool json_semantically_equal(std::string_view left, std::string_view right)
{
    return nlohmann::json::parse(left) == nlohmann::json::parse(right);
}
